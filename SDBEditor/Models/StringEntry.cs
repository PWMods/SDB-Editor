using System;

namespace SDBEditor.Models
{
    /// <summary>
    /// Represents a single string entry in an SDB file
    /// </summary>

    public class SharelistEntry
    {
        public uint HashId { get; set; }
        public string Text { get; set; }

        public SharelistEntry()
        {
        }

        public SharelistEntry(uint hashId, string text)
        {
            HashId = hashId;
            Text = text;
        }
    }
    public class StringEntry
    {
        public uint HashId { get; set; }
        public string Text { get; set; }
        public bool Mangled { get; set; }

        public StringEntry(uint hashId, string text, bool mangled = false)
        {
            HashId = hashId;
            Text = text ?? string.Empty; // Ensure text is never null
            Mangled = mangled;
        }
    }
}