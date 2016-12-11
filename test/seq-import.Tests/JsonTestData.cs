using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace seq_import.Tests
{
    class JsonTestData
    {
        private static JObject ToJObject(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                return JObject.Load(reader);
            }
        }

        public static JObject GetDefaultJson()
        {
            string json =
   @"{
	""Timestamp"": ""2016-06-07T13:44:57.8532799+10:00"",
	""Level"": ""Information"",
	""MessageTemplate"": ""Hello, {@User}, {N:x8} at {Now}"",
	""Exception"": ""System.Exception: Exception of type \""System.Exception\"" was thrown"",
	""Properties"": {
		""User"": {
			""Name"": ""nblumhardt"",
			""Tags"": [1, 2, 3]
		},
		""N"": 123,
		""Now"": ""2016-06-07T13:44:57.8532799+10:00""
	},
	""Renderings"": {
		""N"": [{
			""Format"": ""x8"",
			""Rendering"": ""0000007b""
		}]
	}
}";

            return ToJObject(json);
        }

        public static JObject GetCompactJson()
        {
            var json = @"{
	""@t"": ""2016-06-07T03:44:57.8532799Z"",
	""@mt"": ""Hello, {@User}, {N:x8} at {Now}"",
	""@r"": [""0000007b""],
	""@x"": ""System.Exception: Exception of type \""System.Exception\"" was thrown"",
	""User"": {
		""Name"": ""nblumhardt"",
		""Tags"": [1, 2, 3]
	},
	""N"": 123,
	""Now"": ""2016-06-07T13:44:57.8532799+10:00""
}";

            return ToJObject(json);
        }
    }
}
