using System;
using System.Collections.Generic;

namespace TaruruutoCLI
{
    public class TableParser
    {
        public Dictionary<byte, string> hexToText = new();
        public Dictionary<string, byte> textToHex = new();
        public Dictionary<string, byte[]> namesToHex = new();

        public void LoadTable(string filePath)
        {
            string[] lines = System.IO.File.ReadAllLines(filePath);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                //Text is in the format of 0f=A

                byte hex = byte.Parse(line.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                string text = line.Substring(3);
                hexToText[hex] = text;
                textToHex[text] = hex;
            }
        }

        public void LoadNames(string filePath)
        {
            if (!System.IO.File.Exists(filePath)) return;
            string json = System.IO.File.ReadAllText(filePath);
            var names = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(json);
            if (names != null)
            {
                foreach (var kvp in names)
                {
                    byte[] bytes = new byte[kvp.Value.Length];
                    for (int i = 0; i < kvp.Value.Length; i++)
                    {
                        bytes[i] = Convert.ToByte(kvp.Value[i], 16);
                    }
                    namesToHex[kvp.Key] = bytes;
                }
            }
        }
    }
}
