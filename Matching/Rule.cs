using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Toggl_Exist.Matching
{
    public class Rule
    {
        public readonly JObject Pattern;

        public Rule(JToken pattern)
        {
            Pattern = pattern.ToObject<JObject>();
        }

        public override string ToString()
        {
            return $"Rule({Pattern.ToString(Formatting.None)})";
        }

        public bool IsMatch(JObject target)
        {
            return IsMatch(Pattern["$if"], target);
        }

        bool IsMatch(JToken pattern, JToken value)
        {
            switch (pattern.Type)
            {
                case JTokenType.Object:
                    if (pattern["$regex"] != null)
                    {
                        return new Regex(pattern["$regex"].ToObject<string>()).IsMatch(value.ToObject<string>());
                    }
                    return pattern.Children().OfType<JProperty>().All(prop =>
                    {
                        if (prop.Name.StartsWith("$and"))
                        {
                            if (prop.Value.Type != JTokenType.Array)
                            {
                                throw new InvalidOperationException($"Expect array; got {prop.Value.Type}");
                            }
                            return prop.Value.Children().Any(cp => IsMatch(cp, value));
                        }
                        if (prop.Name.StartsWith("$or"))
                        {
                            if (prop.Value.Type != JTokenType.Array)
                            {
                                throw new InvalidOperationException($"Expect array; got {prop.Value.Type}");
                            }
                            return prop.Value.Children().Any(cp => IsMatch(cp, value));
                        }
                        if (prop.Name.StartsWith("$contains"))
                        {
                            if (value.Type != JTokenType.Array)
                            {
                                throw new InvalidOperationException($"Expect array; got {value.Type}");
                            }
                            return value.Children().Any(cv => IsMatch(prop.Value, cv));
                        }
                        if (prop.Name.StartsWith("$"))
                        {
                            throw new InvalidOperationException($"Expected valid operator; got {prop.Name}");
                        }
                        return IsMatch(prop.Value, value[prop.Name]);
                    });
                case JTokenType.String:
                    return pattern.ToObject<string>() == value.ToObject<string>();
                default:
                    throw new InvalidOperationException($"Expected object; got {pattern.Type}");
            }
        }
    }
}
