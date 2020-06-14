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
                        return new Regex(pattern["$regex"].ToObject<string>(), RegexOptions.IgnoreCase).IsMatch(value.ToObject<string>());
                    }
                    if (pattern["$empty"] != null)
                    {
                        return pattern["$empty"].ToObject<bool>() == (value.Children().Count() == 0);
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
                        if (prop.Name.StartsWith("$not"))
                        {
                            return !IsMatch(prop.Value, value);
                        }
                        if (prop.Name.StartsWith("$"))
                        {
                            throw new InvalidOperationException($"Expected valid operator; got {prop.Name}");
                        }
                        return IsMatch(prop.Value, value[prop.Name]);
                    });
                case JTokenType.String:
                    return 0 == StringComparer.CurrentCultureIgnoreCase.Compare(pattern.ToObject<string>(), value.ToObject<string>());
                case JTokenType.Array:
                    if (value.Type != JTokenType.Array)
                    {
                        throw new InvalidOperationException($"Expect array; got {value.Type}");
                    }
                    var patternArray = pattern.ToArray();
                    var valueArray = value.ToArray();
                    return patternArray.Length == valueArray.Length &&
                        patternArray.All(p => valueArray.Any(v => IsMatch(p, v))) &&
                        valueArray.All(v => patternArray.Any(p => IsMatch(p, v)));
                default:
                    throw new InvalidOperationException($"Expected object; got {pattern.Type}");
            }
        }
    }
}
