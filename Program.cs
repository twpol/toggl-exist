using CLP = CommandLineParser;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Toggl_Exist.Matching;

namespace Toggl_Exist
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new CLP.Arguments.FileArgument('c', "config")
            {
                ForcedDefaultValue = new FileInfo("config.json")
            };

            var commandLineParser = new CLP.CommandLineParser()
            {
                Arguments = {
                    config,
                }
            };

            try
            {
                commandLineParser.ParseCommandLine(args);

                Main(new ConfigurationBuilder()
                    .AddJsonFile(config.Value.FullName, true)
                    .Build(), LoadConfig(config.Value.FullName))
                    .Wait();
            }
            catch (CLP.Exceptions.CommandLineException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        static JObject LoadConfig(string fileName)
        {
            using (StreamReader reader = File.OpenText(fileName))
            {
                return (JObject)JToken.ReadFrom(new JsonTextReader(reader));
            }
        }

        static async Task Main(IConfigurationRoot config, JObject configJson)
        {
            var togglConfig = config.GetSection("Toggl");
            var toggl = new Toggl.Query(togglConfig["ApiToken"], togglConfig.GetSection("Workspaces").GetChildren().Select(s => s.Value).ToList());

            var existConfig = config.GetSection("Exist");
            var exist = new Exist.Query(existConfig["AccessToken"]);

            var tzOffset = configJson["TimeZoneOffset"].Value<int>();
            var rules = configJson["Rules"].Select(rule => new Rule(rule));

            var existTags = await exist.GetTags();
            var timeEntries = await toggl.GetDetails(existTags);

            var counts = new Dictionary<string, int>();
            var durations = new Dictionary<string, TimeSpan>();
            var lastDay = DateTime.MinValue;
            foreach (var timeEntry in timeEntries)
            {
                var day = timeEntry.start.AddMinutes(tzOffset).Date;
                if (day != lastDay)
                {
                    if (counts.Count > 0)
                    {
                        Console.WriteLine($"{lastDay.ToString("yyyy-MM-dd")} {String.Join(" ", counts.Select(kvp => $"{kvp.Key} = {kvp.Value}"))}");
                        foreach (var attr in counts.Keys)
                        {
                            await exist.SetAttribute(lastDay, attr, (int)counts[attr]);
                        }
                    }
                    counts.Clear();
                    if (durations.Count > 0)
                    {
                        Console.WriteLine($"{lastDay.ToString("yyyy-MM-dd")} {String.Join(" ", durations.Select(kvp => $"{kvp.Key} = {kvp.Value.ToString(@"hh\:mm")}"))}");
                        foreach (var attr in durations.Keys)
                        {
                            await exist.SetAttribute(lastDay, attr, (int)durations[attr].TotalMinutes);
                        }
                    }
                    durations.Clear();
                }
                var tags = new HashSet<string>();
                foreach (var rule in rules)
                {
                    if (rule.IsMatch(JObject.FromObject(timeEntry)))
                    {
                        if (rule.Pattern["$set"]["tags"] != null)
                        {
                            foreach (var tag in rule.Pattern["$set"]["tags"].ToObject<string[]>())
                            {
                                tags.Add(tag);
                            }
                        }
                        if (rule.Pattern["$set"]["matchingTags"] != null && rule.Pattern["$set"]["matchingTags"].ToObject<bool>() == true)
                        {
                            foreach (var tag in timeEntry.tags)
                            {
                                if (existTags.Contains(tag, StringComparer.CurrentCultureIgnoreCase))
                                {
                                    tags.Add(tag);
                                }
                            }
                        }
                        if (rule.Pattern["$set"]["count_attribute"] != null)
                        {
                            var attr = rule.Pattern["$set"]["count_attribute"].ToObject<string>();
                            if (!counts.ContainsKey(attr))
                            {
                                counts[attr] = 0;
                            }
                            counts[attr]++;
                        }
                        if (rule.Pattern["$set"]["duration_attribute"] != null)
                        {
                            var attr = rule.Pattern["$set"]["duration_attribute"].ToObject<string>();
                            if (!durations.ContainsKey(attr))
                            {
                                durations[attr] = TimeSpan.Zero;
                            }
                            durations[attr] += timeEntry.duration;
                        }
                    }
                }
                if (lastDay != day && lastDay != DateTime.MinValue) Console.WriteLine("------------------------------");
                lastDay = day;
                Console.WriteLine($"{day.ToString("yyyy-MM-dd")} {timeEntry.start.TimeOfDay.ToString(@"hh\:mm")}-{timeEntry.end.TimeOfDay.ToString(@"hh\:mm")} ({(timeEntry.end - timeEntry.start).ToString(@"hh\:mm")}) {timeEntry.project}/{timeEntry.description} [{String.Join(", ", timeEntry.tags)}] --> [{String.Join(", ", tags)}]");
                await exist.AddTags(day, tags);
            }
            // Do not set durations here, as they might be incomplete!
        }
    }
}
