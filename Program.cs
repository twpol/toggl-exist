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
            var verbose = new CLP.Arguments.SwitchArgument('v', "verbose", false);
            var date = new CLP.Arguments.ValueArgument<int>('d', "date");

            var commandLineParser = new CLP.CommandLineParser()
            {
                Arguments = {
                    config,
                    verbose,
                    date,
                }
            };

            try
            {
                commandLineParser.ParseCommandLine(args);

                Main(new ConfigurationBuilder()
                    .AddJsonFile(config.Value.FullName, true)
                    .Build(), LoadConfig(config.Value.FullName), verbose.Value, date.Value)
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

        static async Task Main(IConfigurationRoot config, JObject configJson, bool verbose, int date)
        {
            var togglConfig = config.GetSection("Toggl");
            var toggl = new Toggl.Query(togglConfig["ApiToken"], togglConfig.GetSection("Workspaces").GetChildren().Select(s => s.Value).ToList());

            var existConfig = config.GetSection("Exist");
            var exist = new Exist.Query(existConfig["AccessToken"]);

            var tzOffset = configJson["TimeZoneOffset"].Value<int>();
            var rules = configJson["Rules"].Select(rule => new Rule(rule));

            var existTags = await exist.GetTags();
            var togglQuery = new Dictionary<string, string>();
            var targetDate = date != 0 ? DateTimeOffset.Now.Date.AddDays(-date) : DateTimeOffset.MaxValue;
            if (date != 0)
            {
                togglQuery["since"] = targetDate.ToString("yyyy-MM-dd");
                togglQuery["until"] = targetDate.AddDays(1).ToString("yyyy-MM-dd");
            }
            var timeEntries = await toggl.GetDetails(existTags, togglQuery);

            var counts = new Dictionary<string, int>();
            var durations = new Dictionary<string, TimeSpan>();
            var tags = new HashSet<string>();
            var lastDay = DateTime.MinValue;
            foreach (var timeEntry in timeEntries)
            {
                var day = timeEntry.start.AddMinutes(tzOffset).Date;
                if (day > targetDate)
                {
                    continue;
                }
                if (day != lastDay)
                {
                    if (lastDay != DateTime.MinValue)
                    {
                        Console.WriteLine($"{lastDay.ToString("yyyy-MM-dd")} {String.Join(" ", counts.Select(kvp => $"{kvp.Key}={kvp.Value}"))} {String.Join(" ", durations.Select(kvp => $"{kvp.Key}={kvp.Value.ToString(@"hh\:mm")}"))} {String.Join(" ", tags.Select(tag => $"tag=\"{tag}\""))}");
                        foreach (var attr in counts.Keys)
                        {
                            await exist.SetAttribute(lastDay, attr, (int)counts[attr]);
                        }
                        foreach (var attr in durations.Keys)
                        {
                            await exist.SetAttribute(lastDay, attr, (int)durations[attr].TotalMinutes);
                        }
                        await exist.AddTags(day, tags);
                    }
                    ResetAttributes(rules, counts, durations, tags);
                }
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
                            counts[rule.Pattern["$set"]["count_attribute"].ToObject<string>()]++;
                        }
                        if (rule.Pattern["$set"]["duration_attribute"] != null)
                        {
                            durations[rule.Pattern["$set"]["duration_attribute"].ToObject<string>()] += timeEntry.duration;
                        }
                    }
                }
                if (verbose)
                {
                    if (day < lastDay) Console.WriteLine("------------------------------");
                    Console.WriteLine($"{day.ToString("yyyy-MM-dd")} {timeEntry.start.TimeOfDay.ToString(@"hh\:mm")}-{timeEntry.end.TimeOfDay.ToString(@"hh\:mm")} ({(timeEntry.end - timeEntry.start).ToString(@"hh\:mm")}) {timeEntry.project}/{timeEntry.description} [{String.Join(", ", timeEntry.tags)}]");
                }
                lastDay = day;
            }
            // Do not set durations here, as they might be incomplete!
        }

        static void ResetAttributes(IEnumerable<Rule> rules, Dictionary<string, int> counts, Dictionary<string, TimeSpan> durations, HashSet<string> tags)
        {
            counts.Clear();
            durations.Clear();
            tags.Clear();
            foreach (var rule in rules)
            {
                if (rule.Pattern["$set"]["count_attribute"] != null)
                {
                    counts[rule.Pattern["$set"]["count_attribute"].ToObject<string>()] = 0;
                }
                if (rule.Pattern["$set"]["duration_attribute"] != null)
                {
                    durations[rule.Pattern["$set"]["duration_attribute"].ToObject<string>()] = TimeSpan.Zero;
                }
            }
        }
    }
}
