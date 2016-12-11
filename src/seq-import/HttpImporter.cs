using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace seq_import
{
    class HttpImporter
    {
        const string ApiKeyHeaderName = "X-Seq-ApiKey";
        const string BulkUploadResource = "api/events/raw";

        static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(false);

        readonly LogBuffer _logBuffer;
        readonly SeqImportConfig _importConfig;
        readonly HttpClient _httpClient;

        public HttpImporter(LogBuffer logBuffer, SeqImportConfig importConfig)
        {
            if (logBuffer == null) throw new ArgumentNullException(nameof(logBuffer));
            if (importConfig == null) throw new ArgumentNullException(nameof(importConfig));

            if (string.IsNullOrWhiteSpace(importConfig.ServerUrl))
                throw new ArgumentException("The destination Seq server URL must be provided.");

            _logBuffer = logBuffer;
            _importConfig = importConfig;

            var baseUri = importConfig.ServerUrl;
            if (!baseUri.EndsWith("/"))
                baseUri += "/";

            _httpClient = new HttpClient { BaseAddress = new Uri(baseUri) };
        }

        public async Task Import()
        {
            var sendingSingles = 0;
            var sent = 0L;

            var eventPayloadLimitBytes = _importConfig.CompactJson ? _importConfig.RawPayloadLimitBytes : _importConfig.RawPayloadLimitBytes - 13; // prologue and epilogue overhead, none for compactJson
            var overheadPerEvent = _importConfig.CompactJson ? 2 : 1; //compact: \r\n vs. defaultJson: ,

            var payloadBuffer = new MemoryStream();
            var availableBuffer = new List<LogBufferEntry>();
            do
            {
                payloadBuffer.SetLength(0);
                availableBuffer.Clear();

                _logBuffer.Peek(availableBuffer, (int)eventPayloadLimitBytes, overheadPerEvent, (int)_importConfig.EventBodyLimitBytes);
                if (availableBuffer.Count == 0)
                {
                    break;
                }

                ulong lastIncluded;
                if (_importConfig.CompactJson)
                {
                    MakeCompactJsonPayload(availableBuffer, sendingSingles > 0, payloadBuffer, out lastIncluded);
                }
                else
                {
                    MakeDefaultJsonPayload(availableBuffer, sendingSingles > 0, payloadBuffer, out lastIncluded);
                }
                var len = payloadBuffer.Length;

                payloadBuffer.Position = 0;
                var content = new StreamContent(new UnclosableStreamWrapper(payloadBuffer));
                content.Headers.ContentType = new MediaTypeHeaderValue(_importConfig.CompactJson ? "application/vnd.serilog.clef" : "application/json")
                {
                    CharSet = Encoding.UTF8.WebName
                };

                if (!string.IsNullOrWhiteSpace(_importConfig.ApiKey))
                    content.Headers.Add(ApiKeyHeaderName, _importConfig.ApiKey);

                var result = await _httpClient.PostAsync(BulkUploadResource, content);
                if (result.IsSuccessStatusCode)
                {
                    sent += len;
                    Log.Information("Sent {TotalBytes} total bytes uploaded", sent);

                    _logBuffer.Dequeue(lastIncluded);
                    if (sendingSingles > 0)
                        sendingSingles--;
                }
                else if (result.StatusCode == HttpStatusCode.BadRequest ||
                    result.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                {
                    if (sendingSingles != 0)
                    {
                        payloadBuffer.Position = 0;
                        var payloadText = new StreamReader(payloadBuffer, Encoding.UTF8).ReadToEnd();
                        Log.Error("HTTP shipping failed with {StatusCode}: {Result}; payload was {InvalidPayload}", result.StatusCode, await result.Content.ReadAsStringAsync(), payloadText);
                        _logBuffer.Dequeue(lastIncluded);
                        sendingSingles = 0;
                    }
                    else
                    {
                        Log.Warning("Batch failed with {StatusCode}, breaking out the first hundred events to send individually...", result.StatusCode);

                        // Unscientific (should "binary search" in batches) but sending the next
                        // hundred events singly should flush out the problematic one.
                        sendingSingles = 100;
                    }
                }
                else
                {
                    Log.Error("Received failed HTTP shipping result {StatusCode}: {Result}", result.StatusCode, await result.Content.ReadAsStringAsync());
                    break;
                }
            }
            while (true);
        }

        private static byte[] eventPrologue = _utf8NoBom.GetBytes("{\"Events\":[");
        private static byte[] eventEpilogue = _utf8NoBom.GetBytes("]}");
        private static byte[] comma = _utf8NoBom.GetBytes(",");

        void MakeDefaultJsonPayload(List<LogBufferEntry> entries, bool oneOnly, MemoryStream payloadBuffer, out ulong lastIncluded)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0) throw new ArgumentException("Must contain entries");
            lastIncluded = 0;

            payloadBuffer.Write(eventPrologue, 0, eventPrologue.Length);

            bool writeComma = false;
            foreach (var logBufferEntry in entries)
            {
                if (writeComma)
                {
                    payloadBuffer.Write(comma, 0, comma.Length);
                }
                writeComma = true;

                payloadBuffer.Write(logBufferEntry.Value, 0, logBufferEntry.Value.Length);

                lastIncluded = logBufferEntry.Key;

                if (oneOnly)
                    break;
            }

            payloadBuffer.Write(eventEpilogue, 0, eventEpilogue.Length);
        }

        private static byte[] newline = _utf8NoBom.GetBytes("\r\n");

        void MakeCompactJsonPayload(List<LogBufferEntry> entries, bool oneOnly, MemoryStream payloadBuffer, out ulong lastIncluded)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0) throw new ArgumentException("Must contain entries");
            lastIncluded = 0;

            bool writeNewline = false;
            foreach (var logBufferEntry in entries)
            {
                if (writeNewline)
                {
                    payloadBuffer.Write(newline, 0, newline.Length);
                }
                writeNewline = true;

                payloadBuffer.Write(logBufferEntry.Value, 0, logBufferEntry.Value.Length);

                lastIncluded = logBufferEntry.Key;

                if (oneOnly)
                    break;
            }
        }
    }
}
