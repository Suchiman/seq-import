using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace seq_import.Tests
{
    [TestClass]
    public class LogFileConversionTests
    {
        private static JObject ToJObject(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                return JObject.Load(reader);
            }
        }

        [TestMethod]
        public void TestConversionToCompact()
        {
            var compactJson = EventConverter.ConvertToCompactJson(JsonTestData.GetDefaultJson());

            Assert.AreEqual(compactJson["@t"].ToString(), "2016-06-07T03:44:57.8532799Z");
            Assert.AreEqual(compactJson["@mt"].ToString(), "Hello, {@User}, {N:x8} at {Now}");
            Assert.AreEqual(compactJson["@x"].ToString(), "System.Exception: Exception of type \"System.Exception\" was thrown");
            Assert.AreEqual(compactJson["User"]["Name"].ToString(), "nblumhardt");
            Assert.IsTrue(compactJson["User"]["Tags"].Values<int>().SequenceEqual(new int[] { 1, 2, 3 }));
            Assert.AreEqual(compactJson["N"].ToString(), "123");
            Assert.AreEqual(compactJson["Now"].ToString(), "2016-06-07T13:44:57.8532799+10:00");
        }

        [TestMethod]
        public void TestConversionToDefaultJson()
        {
            var defaultJson = EventConverter.ConvertToDefaultJson(JsonTestData.GetCompactJson());

            Assert.AreEqual(defaultJson["Timestamp"].ToString(), "2016-06-07T03:44:57.8532799+00:00");
            Assert.AreEqual(defaultJson["Level"].ToString(), "Information");
            Assert.AreEqual(defaultJson["MessageTemplate"].ToString(), "Hello, {@User}, {N:x8} at {Now}");
            Assert.AreEqual(defaultJson["Exception"].ToString(), "System.Exception: Exception of type \"System.Exception\" was thrown");
            Assert.AreEqual(defaultJson["Properties"]["User"]["Name"].ToString(), "nblumhardt");
            Assert.IsTrue(defaultJson["Properties"]["User"]["Tags"].Values<int>().SequenceEqual(new int[] { 1, 2, 3 }));
            Assert.AreEqual(defaultJson["Properties"]["N"].ToString(), "123");
            Assert.AreEqual(defaultJson["Properties"]["Now"].ToString(), "2016-06-07T13:44:57.8532799+10:00");
        }
    }
}
