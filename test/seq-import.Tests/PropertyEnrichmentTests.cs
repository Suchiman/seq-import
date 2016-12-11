using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace seq_import.Tests
{
    [TestClass]
    public class PropertyEnrichmentTests
    {
        [TestMethod]
        public void TestEnrichCompactJson()
        {
            var additionalProperties = new Dictionary<string, object>
            {
                ["@x"] = "test",
                ["Number"] = 1
            };

            JObject logEvent = JsonTestData.GetCompactJson();
            PropertyEnricher.AddPropertiesToCompactJson(logEvent, additionalProperties);

            Assert.AreEqual(logEvent["@x"].ToString(), "System.Exception: Exception of type \"System.Exception\" was thrown");
            Assert.AreEqual(logEvent["@@x"].ToString(), "test");
            Assert.AreEqual(logEvent["Number"].ToString(), "1");
        }

        [TestMethod]
        public void TestEnrichDefaultJson()
        {
            var additionalProperties = new Dictionary<string, object>
            {
                ["@x"] = "test",
                ["Number"] = 1
            };

            JObject logEvent = JsonTestData.GetDefaultJson();
            PropertyEnricher.AddPropertiesToDefaultJson(logEvent, additionalProperties);

            Assert.AreEqual(logEvent["Properties"]["@x"].ToString(), "test");
            Assert.AreEqual(logEvent["Properties"]["Number"].ToString(), "1");
        }
    }
}