using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Serilog;

namespace seq_import
{
    class LogBuffer
    {
        private readonly SortedDictionary<ulong, byte[]> _entries = new SortedDictionary<ulong, byte[]>();
        private readonly StreamingEventReader _eventReader;
        private readonly IEnumerator<LogBufferEntry> _eventEnumerator;

        public LogBuffer(StreamingEventReader eventReader)
        {
            if (eventReader == null) throw new ArgumentNullException(nameof(eventReader));

            _eventReader = eventReader;
            _eventEnumerator = eventReader.GetEnumerator();
        }

        public void Peek(List<LogBufferEntry> availableBuffers, int maxValueBytesHint, int overheadPerEvent, int eventBodyLimitBytes)
        {
            long currentLength = -overheadPerEvent;

            foreach (var current in _entries)
            {
                var entry = new LogBufferEntry
                {
                    Key = current.Key,
                    Value = current.Value
                };

                currentLength += overheadPerEvent;
                currentLength += entry.Value.Length;
                if (availableBuffers.Count != 0 && currentLength > maxValueBytesHint)
                    break;

                availableBuffers.Add(entry);
            }

            while (_eventEnumerator.MoveNext())
            {
                LogBufferEntry current = _eventEnumerator.Current;
                if (current.Value.Length > eventBodyLimitBytes)
                {
                    Log.Warning("Oversized event will be skipped, {Payload}", Encoding.UTF8.GetString(current.Value));
                    continue;
                }

                currentLength += overheadPerEvent;
                currentLength += current.Value.Length;

                _entries.Add(current.Key, current.Value);
                if (availableBuffers.Count != 0 && currentLength > maxValueBytesHint)
                    break;

                availableBuffers.Add(current);
            }
        }

        public void Dequeue(ulong toKey)
        {
            while (_entries.Count > 0)
            {
                var current = _entries.First();
                if (current.Key > toKey)
                    break;

                _entries.Remove(current.Key);
            }
        }
    }
}
