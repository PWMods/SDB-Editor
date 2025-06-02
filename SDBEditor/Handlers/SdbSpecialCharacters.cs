using System;
using System.Text;
using System.Collections.Generic;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Handles decoding of special character sequences in SDB files,
    /// matching the behavior of the legacy Python tool
    /// </summary>
    public static class SdbSpecialCharacters
    {
        // Maps raw byte patterns to Unicode characters
        private static readonly Dictionary<byte, char> SpecialCharMap = new Dictionary<byte, char>
        {
            { 0x01, '□' }, // Square
            { 0x02, '△' }, // Triangle
            { 0x03, '○' }, // Circle
            { 0x04, '⨂' }, // Circle with X (HOLD symbol)
            { 0x05, '×' }, // X / Cross
            { 0x06, '+' }, // Plus
            { 0x07, '▷' }, // Play/Start
            { 0x08, '◁' }, // Back
        };

        /// <summary>
        /// Examines a string for special character sequences and formats them correctly
        /// </summary>
        /// <param name="text">The string to process</param>
        /// <returns>A string with correctly formatted special characters</returns>
        public static string ProcessSpecialCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Process specific patterns
            string result = text;

            // Special HOLD handling
            if (result.Contains("HOLD □"))
                result = result.Replace("HOLD □", "HOLD ⨂");

            if (result.Contains("+ HOLD"))
                result = result.Replace("+ HOLD □", "+ HOLD ⨂");

            // Handle special sequences
            if (result.Contains("□"))
                result = result.Replace("□", "□");  // Ensure proper square

            if (result.Contains("△"))
                result = result.Replace("△", "△");  // Ensure proper triangle

            if (result.Contains("○"))
                result = result.Replace("○", "○");  // Ensure proper circle

            if (result.Contains("⨂"))
                result = result.Replace("⨂", "⨂");  // Ensure proper HOLD symbol

            return result;
        }

        /// <summary>
        /// Special case handler for examining what's in a specific string
        /// </summary>
        public static string DiagnoseString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "Empty text";

            StringBuilder diagnosis = new StringBuilder();
            diagnosis.AppendLine($"Total characters: {text.Length}");

            // Show the Unicode codepoints
            diagnosis.AppendLine("Character codepoints:");
            for (int i = 0; i < text.Length; i++)
            {
                diagnosis.AppendLine($"Char {i}: '{text[i]}' (U+{(int)text[i]:X4})");
            }

            return diagnosis.ToString();
        }
    }
}