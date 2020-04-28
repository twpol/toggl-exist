using CLP = CommandLineParser;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Toggl_Exist.Matching;
using Toggl_Exist.Toggl;

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
            var toggl = config.GetSection("Toggl");
            var timeEntries = await new Query(toggl["ApiToken"], toggl["Workspace"]).GetDetails();

            var tzOffset = configJson["TimeZoneOffset"].Value<int>();
            var rules = configJson["Rules"].Select(rule => new Rule(rule));

            foreach (var timeEntry in timeEntries)
            {
                var day = timeEntry.start.AddMinutes(tzOffset).Date;
                var matched = false;
                foreach (var rule in rules)
                {
                    if (rule.IsMatch(JObject.FromObject(timeEntry)))
                    {
                        matched = true;
                        Console.WriteLine($"{day} : {timeEntry.start.TimeOfDay}-{timeEntry.end.TimeOfDay} : {timeEntry.project}/{timeEntry.description} : {String.Join(", ", timeEntry.tags)} --> {rule.Pattern["$set"].ToString(Formatting.None)}");
                    }
                }
                if (!matched)
                {
                    Console.WriteLine($"{day} : {timeEntry.start.TimeOfDay}-{timeEntry.end.TimeOfDay} : {timeEntry.project}/{timeEntry.description} : {String.Join(", ", timeEntry.tags)}");
                }
            }
        }
    }
}
