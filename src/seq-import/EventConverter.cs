using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace seq_import
{
    public class EventConverter
    {
        /*
{
	"Timestamp": "2016-06-07T13:44:57.8532799+10:00",
	"Level": "Information",
	"MessageTemplate": "Hello, {@User}, {N:x8} at {Now}",
	"Exception": "System.Exception: Exception of type \"System.Exception\" was thrown",
	"Properties": {
		"User": {
			"Name": "nblumhardt",
			"Tags": [1, 2, 3]
		},
		"N": 123,
		"Now": "2016-06-07T13:44:57.8532799+10:00"
	},
	"Renderings": {
		"N": [{
			"Format": "x8",
			"Rendering": "0000007b"
		}]
	}
}

{
	"@t": "2016-06-07T03:44:57.8532799Z",
	"@mt": "Hello, {@User}, {N:x8} at {Now}",
	"@r": ["0000007b"],
    "@x": "System.Exception: Exception of type \"System.Exception\" was thrown",
	"User": {
		"Name": "nblumhardt",
		"Tags": [1,
		2,
		3]
	},
	"N": 123,
	"Now": "2016-06-07T13:44:57.8532799+10:00"
}
        */

        public static JObject ConvertToCompactJson(JObject entry)
        {
            var compact = new JObject();

            var offset = DateTimeOffset.ParseExact(entry["Timestamp"].ToString(), "o", CultureInfo.InvariantCulture);
            compact["@t"] = offset.UtcDateTime.ToString("O");

            if (entry["Level"].ToString() != "Information")
            {
                compact["@l"] = entry["Level"];
            }

            if (entry["Message"] != null)
            {
                compact["@m"] = entry["Message"];
            }

            if (entry["MessageTemplate"] != null)
            {
                compact["@mt"] = entry["MessageTemplate"];
            }

            if (entry["Exception"] != null)
            {
                compact["@x"] = entry["Exception"];
            }

            if (entry["Properties"] != null)
            {
                foreach (var property in entry["Properties"])
                {
                    compact.Add(property);
                }
            }

            return compact;
        }

        public static JObject ConvertToDefaultJson(JObject compact)
        {
            var entry = new JObject();

            var timestamp = DateTime.ParseExact(compact["@t"].ToString(), "O", CultureInfo.InvariantCulture);
            entry["Timestamp"] = new DateTimeOffset(timestamp).ToUniversalTime().ToString("o");

            entry["Level"] = compact["@l"]?.ToString() ?? "Information";

            if (compact["@m"] != null)
            {
                entry["Message"] = compact["@m"];
            }

            if (compact["@mt"] != null)
            {
                entry["MessageTemplate"] = compact["@mt"];
            }

            if (compact["@x"] != null)
            {
                entry["Exception"] = compact["@x"];
            }

            var properties = new JObject();
            entry["Properties"] = properties;

            foreach (var property in compact.OfType<JProperty>())
            {
                switch (property.Name)
                {
                    case "@t":
                    case "@m":
                    case "@mt":
                    case "@l":
                    case "@x":
                    case "@i":
                    case "@r":
                        continue;
                    default:
                        properties.Add(property);
                        break;
                }
            }

            return entry;
        }

        public static JObject PassThrough(JObject input)
        {
            return input;
        }
    }
}
