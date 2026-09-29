using System.Collections.Generic;

namespace Castlevania2D.UI
{
    public static class HintJournal
    {
        public readonly struct Entry
        {
            public Entry(string id, string title, string body)
            {
                Id = id;
                Title = title;
                Body = body;
            }

            public string Id { get; }
            public string Title { get; }
            public string Body { get; }
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly HashSet<string> ids = new HashSet<string>();

        public static IReadOnlyList<Entry> Entries => entries;

        public static bool Contains(string id)
        {
            return !string.IsNullOrEmpty(id) && ids.Contains(id);
        }

        public static bool TryAdd(string id, string title, string body)
        {
            if (string.IsNullOrEmpty(id) || ids.Contains(id))
            {
                return false;
            }

            ids.Add(id);
            entries.Add(new Entry(id, title ?? string.Empty, body ?? string.Empty));
            return true;
        }
    }
}
