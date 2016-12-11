using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace seq_import
{
    public class PropertyEnricher
    {
        public static void AddPropertiesToCompactJson(JObject entry, Dictionary<string, object> additionalProperties)
        {
            if (!(additionalProperties?.Count > 0))
                return;

            foreach (var kvp in additionalProperties)
            {
                switch (kvp.Key)
                {
                    case "@t":
                    case "@m":
                    case "@mt":
                    case "@l":
                    case "@x":
                    case "@i":
                    case "@r":
                        entry["@" + kvp.Key] = JToken.FromObject(kvp.Value);
                        break;
                    default:
                        entry[kvp.Key] = JToken.FromObject(kvp.Value);
                        break;
                }
            }
        }

        public static void AddPropertiesToDefaultJson(JObject entry, Dictionary<string, object> additionalProperties)
        {
            if (!(additionalProperties?.Count > 0))
                return;

            var properties = entry["Properties"] as JObject;
            if (properties == null)
            {
                properties = new JObject();
                entry["Properties"] = properties;
            }

            foreach (var kvp in additionalProperties)
            {
                properties[kvp.Key] = JToken.FromObject(kvp.Value);
            }
        }
    }
}
