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
        private Func<JObject, JObject> eventProcessor;
        private Action<JObject, Dictionary<string, object>> propertyEnricher;

        public StreamingEventReader(string file, Dictionary<string, object> tags, Func<JObject, JObject> eventProcessor, Action<JObject, Dictionary<string, object>> propertyEnricher)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (tags == null) throw new ArgumentNullException(nameof(tags));
            if (eventProcessor == null) throw new ArgumentNullException(nameof(eventProcessor));
            if (propertyEnricher == null) throw new ArgumentNullException(nameof(propertyEnricher));

            this.file = file;
            this.tags = tags;
            this.eventProcessor = eventProcessor;
            this.propertyEnricher = propertyEnricher;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<LogBufferEntry> GetEnumerator()
        {
            var encoding = new UTF8Encoding(false);

            using (var r = File.OpenText(file))
            using (var bytes = new MemoryStream())
            using (var writer = new JsonTextWriter(new StreamWriter(bytes, encoding, 1024, leaveOpen: true)))
            {
                int line = 0;
                for (var l = r.ReadLine(); l != null; l = r.ReadLine())
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
