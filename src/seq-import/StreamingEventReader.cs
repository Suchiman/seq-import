using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace seq_import
{
    class StreamingEventReader : IEnumerable<LogBufferEntry>
    {
        // Doing our best here to create a totally "neutral" serializer; may need some more work
        // to avoid special-casing .NET types in any circumstances.
        readonly JsonSerializer _serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            //Binder = new NonBindingSerializationBinder(),
            TypeNameHandling = TypeNameHandling.None
        });

        ulong _nextId = 1;

        private string file;
        private Dictionary<string, object> tags;
        private bool compactOutput;

        public StreamingEventReader(string file, Dictionary<string, object> tags, bool compactOutput)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (tags == null) throw new ArgumentNullException(nameof(tags));

            this.file = file;
            this.tags = tags;
            this.compactOutput = compactOutput;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<LogBufferEntry> GetEnumerator()
        {
            var encoding = new UTF8Encoding(false);

            string fileName = Path.GetFileName(file);
            Log.Information("Opening JSON log file {OriginalFilename}", fileName);

            using (var r = File.OpenText(file))
            using (var bytes = new MemoryStream())
            using (var writer = new JsonTextWriter(new StreamWriter(bytes, encoding, 1024, leaveOpen: true)))
            {
                string firstLine = r.ReadLine();
                bool compactInput = JObject.Parse(firstLine)["@t"] != null;
                Log.Information("Probed JSON log file {OriginalFilename} as {JsonKind}", fileName, compactInput ? "CompactJson" : "DefaultJson");

                Func<JObject, JObject> eventProcessor = compactInput == compactOutput ? EventConverter.PassThrough : compactInput ? (Func<JObject, JObject>)EventConverter.ConvertToDefaultJson : EventConverter.ConvertToCompactJson;
                Action<JObject, Dictionary<string, object>> propertyEnricher = compactOutput ? (Action<JObject, Dictionary<string, object>>)PropertyEnricher.AddPropertiesToCompactJson : PropertyEnricher.AddPropertiesToDefaultJson;

                int line = 0;
                for (var l = firstLine; l != null; l = r.ReadLine())
                {
                    line++;

                    if (l.Length == 0) continue;

                    bytes.SetLength(0);
                    try
                    {
                        var json = _serializer.Deserialize<JObject>(new JsonTextReader(new StringReader(l)));
                        json = eventProcessor(json);
                        propertyEnricher(json, tags);

                        _serializer.Serialize(writer, json);
                        writer.Flush();
                    }
                    catch (JsonException ex)
                    {
                        Log.Error(ex, "Line {Line} is not valid JSON; skipping", line);
                        continue;
                    }

                    yield return new LogBufferEntry { Key = _nextId++, Value = bytes.ToArray() };
                }
            }
        }
    }
}
