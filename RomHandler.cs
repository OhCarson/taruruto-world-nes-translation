using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TaruruutoCLI
{
    public class RomHandler
    {
        public byte[] romData;
        public string romPath;
        public int originalPrgBlocks;
        public int originalChrBlocks;

        public RomHandler(string path)
        {
            this.romPath = path;
            this.romData = File.ReadAllBytes(path);
            this.originalPrgBlocks = romData[4];
            this.originalChrBlocks = romData[5];
        }

        public void ExpandPrgRom()
        {
            int currentPrgBlocks = romData[4];
            if (currentPrgBlocks >= 16) return;
            int extraBlocks = 16 - currentPrgBlocks;

            int currentChrBlocks = romData[5];
            int currentPrgSize = currentPrgBlocks * 16384;
            int currentChrSize = currentChrBlocks * 8192;

            byte[] newRom = new byte[16 + currentPrgSize + (extraBlocks * 16384) + currentChrSize];

            Array.Copy(romData, 0, newRom, 0, 16);
            
            // 1. Copy the entire original PRG ROM (e.g. Banks 0-7)
            Array.Copy(romData, 16, newRom, 16, currentPrgSize);
            
            // 2. Fill the new gap (e.g. Banks 8-14) with 0xFF
            for (int i = 0; i < (extraBlocks - 1) * 16384; i++) 
                newRom[16 + currentPrgSize + i] = 0xFF;
                
            // 3. Duplicate the original last bank (e.g. Bank 7) to the very end (Bank 15)
            Array.Copy(romData, 16 + currentPrgSize - 16384, newRom, 16 + currentPrgSize + ((extraBlocks - 1) * 16384), 16384);
            
            // 4. Copy CHR ROM
            Array.Copy(romData, 16 + currentPrgSize, newRom, 16 + currentPrgSize + (extraBlocks * 16384), currentChrSize);

            newRom[4] = (byte)(currentPrgBlocks + extraBlocks);
            this.romData = newRom;
        }

        public void ExpandChrRom(int extraBlocks)
        {
            int currentPrgBlocks = romData[4];
            int currentChrBlocks = romData[5];
            int currentPrgSize = currentPrgBlocks * 16384;
            int currentChrSize = currentChrBlocks * 8192;

            byte[] newRom = new byte[16 + currentPrgSize + currentChrSize + (extraBlocks * 8192)];

            Array.Copy(romData, 0, newRom, 0, 16 + currentPrgSize + currentChrSize);
            for (int i = 0; i < extraBlocks * 8192; i++) newRom[16 + currentPrgSize + currentChrSize + i] = 0x00;

            newRom[5] = (byte)(currentChrBlocks + extraBlocks);
            this.romData = newRom;
        }

        public string ExtractText(int startOffset, TableParser parser)
        {
            var endTextBytes = new byte[] { 0xFF, 0x00 };
            var extracted = "";
            int offset = startOffset;
            while (offset < this.romData.Length)
            {
                byte b = this.romData[offset];
                if (endTextBytes.Contains(b)) break;
                if (parser.hexToText.ContainsKey(b)) extracted += parser.hexToText[b];
                else extracted += $"[{b:X2}]";
                offset++;
            }
            return extracted;
        }

        public void SaveRom(string filename)
        {
            File.WriteAllBytes(filename, this.romData);
        }

        public void CompileScript(string jsonPath, TableParser parser)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string json = File.ReadAllText(jsonPath);
            List<DynamicTextGroup> scenes = JsonSerializer.Deserialize<List<DynamicTextGroup>>(json, options);

            foreach (var scene in scenes)
            {
                int prgPointerOffset = Convert.ToInt32(scene.Config.PrgPointerOffset, 16);
                romData[prgPointerOffset] = (byte)scene.Config.TargetChrBank;

                int newBankOffset = 16 + (this.originalPrgBlocks * 16384) + (scene.Config.TargetChrBank * 1024);
                int currentOffset = newBankOffset + (scene.Entries.Count * 2);

                for (int i = 0; i < scene.Entries.Count; i++)
                {
                    var entry = scene.Entries[i];

                    int ptrLocation = newBankOffset + (i * 2);
                    int ptrValue = currentOffset - newBankOffset;
                    romData[ptrLocation] = (byte)(ptrValue & 0xFF);
                    romData[ptrLocation + 1] = (byte)((ptrValue >> 8) & 0xFF);

                    List<byte> encoded = EncodeString(entry.TextTranslation, parser);
                    foreach (byte b in encoded) romData[currentOffset++] = b;
                    
                    romData[currentOffset++] = 0x00;
                }

                int totalBytes = currentOffset - newBankOffset;
                Console.WriteLine($"\nTotal Text Length for Scene Bank {scene.Config.TargetChrBank}: {totalBytes}/4096 bytes used.");
            }
        }

        public void PatchTextWithPointers(string jsonFile, TableParser parser, bool deduplicate = false)
        {
            string json = File.ReadAllText(jsonFile);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<DynamicTextGroup> groups = JsonSerializer.Deserialize<List<DynamicTextGroup>>(json, options);

            foreach (var group in groups)
            {
                int currentTextOffset = Convert.ToInt32(group.Config.TextOffset, 16);
                int pointerBase = Convert.ToInt32(group.Config.PointerBase, 16);
                int maxSize = group.Config.MaxSize;
                int startTextOffset = currentTextOffset;

                Dictionary<string, int> stringToOffset = new Dictionary<string, int>();

                foreach (var entry in group.Entries)
                {
                    int pointerAddress = Convert.ToInt32(entry.PointerAddress, 16);
                    if (deduplicate && stringToOffset.ContainsKey(entry.TextTranslation))
                    {
                        int ptrValue = stringToOffset[entry.TextTranslation] - pointerBase;
                        romData[pointerAddress] = (byte)(ptrValue & 0xFF);
                        romData[pointerAddress + 1] = (byte)((ptrValue >> 8) & 0xFF);
                    }
                    else
                    {
                        int ptrValue = currentTextOffset - pointerBase;
                        romData[pointerAddress] = (byte)(ptrValue & 0xFF);
                        romData[pointerAddress + 1] = (byte)((ptrValue >> 8) & 0xFF);

                        if (deduplicate) stringToOffset[entry.TextTranslation] = currentTextOffset;

                        List<byte> encoded = EncodeString(entry.TextTranslation, parser);
                        foreach (byte b in encoded) romData[currentTextOffset++] = b;
                    }
                }

                int totalSize = currentTextOffset - startTextOffset;
                if (totalSize > maxSize) Console.WriteLine($"ERROR: {jsonFile} text size {totalSize} exceeds max size {maxSize}!");
                else Console.WriteLine($"{jsonFile} patched successfully. {totalSize}/{maxSize} bytes used.");
            }
        }

        public void PatchFixedLengthText(string jsonFile, TableParser parser)
        {
            string json = File.ReadAllText(jsonFile);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<UniversalTextEntry> texts = JsonSerializer.Deserialize<List<UniversalTextEntry>>(json, options);

            foreach (var entry in texts)
            {
                if (string.IsNullOrEmpty(entry.Address)) continue;
                int currentOffset = Convert.ToInt32(entry.Address, 16);
                List<byte> encoded = EncodeString(entry.TextTranslation, parser);

                if (entry.MaxLength > 0)
                {
                    if (encoded.Count > entry.MaxLength)
                    {
                        Console.WriteLine($"ERROR: String '{entry.TextTranslation}' too long: {encoded.Count} > {entry.MaxLength} at {entry.Address}");
                        Environment.Exit(1);
                    }
                    while (encoded.Count < entry.MaxLength) encoded.Add(0x02);
                }

                foreach (byte b in encoded) romData[currentOffset++] = b;
            }
            Console.WriteLine($"{jsonFile} patched successfully.");
        }

        private List<byte> EncodeString(string text, TableParser parser)
        {
            List<byte> outBytes = new List<byte>();
            for (int i = 0; i < text.Length;)
            {
                if (text[i] == '[')
                {
                    int end = text.IndexOf(']', i);
                    if (end != -1)
                    {
                        string token = text.Substring(i, end - i + 1);
                        string innerToken = text.Substring(i + 1, end - i - 1);

                        if (parser.namesToHex.ContainsKey(innerToken)) outBytes.AddRange(parser.namesToHex[innerToken]);
                        else if (parser.namesToHex.ContainsKey(token)) outBytes.AddRange(parser.namesToHex[token]);
                        else if (parser.textToHex.ContainsKey(token)) outBytes.Add(parser.textToHex[token]);
                        else if (token.Length == 4) outBytes.Add(Convert.ToByte(token.Substring(1, 2), 16));
                        else outBytes.Add(0x02);
                        
                        i = end + 1;
                        continue;
                    }
                }
                string c = text[i].ToString();
                if (i + 1 < text.Length && text.Substring(i, 2) == "'s")
                {
                    outBytes.Add(parser.textToHex.ContainsKey("'") ? parser.textToHex["'"] : (byte)0x02);
                    outBytes.Add(parser.textToHex.ContainsKey("s") ? parser.textToHex["s"] : (byte)0x02);
                    i += 2;
                    continue;
                }
                if (parser.textToHex.ContainsKey(c)) outBytes.Add(parser.textToHex[c]);
                else outBytes.Add(0x02);
                i++;
            }
            return outBytes;
        }

        public void ExtractFixedLengthText(string jsonFile, TableParser parser)
        {
            if (!File.Exists(jsonFile)) return;
            string json = File.ReadAllText(jsonFile);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
            List<UniversalTextEntry> texts = JsonSerializer.Deserialize<List<UniversalTextEntry>>(json, options);

            foreach (var entry in texts)
            {
                if (string.IsNullOrEmpty(entry.Address) || entry.MaxLength <= 0) continue;
                int currentOffset = Convert.ToInt32(entry.Address, 16);
                
                string extracted = "";
                for (int i = 0; i < entry.MaxLength; i++)
                {
                    byte b = romData[currentOffset + i];
                    if (parser.hexToText.ContainsKey(b)) extracted += parser.hexToText[b];
                    else if (b == 0x3C) extracted += "[3C]";
                    else if (b == 0x3D) extracted += "[3D]";
                    else if (b == 0x3F) extracted += "[3F]";
                    else extracted += $"[{b:X2}]";
                }
                entry.TextOriginal = extracted;
            }

            File.WriteAllText(jsonFile, JsonSerializer.Serialize(texts, options));
            Console.WriteLine($"{jsonFile} extracted successfully.");
        }

        public void ExtractTextWithPointers(string jsonFile, TableParser parser)
        {
            if (!File.Exists(jsonFile)) return;
            string json = File.ReadAllText(jsonFile);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
            List<DynamicTextGroup> groups = JsonSerializer.Deserialize<List<DynamicTextGroup>>(json, options);

            foreach (var group in groups)
            {
                int pointerBase = Convert.ToInt32(group.Config.PointerBase, 16);
                
                // Determine the terminator byte, defaulting to 0x3F
                byte terminatorByte = 0x3F;
                if (!string.IsNullOrEmpty(group.Config.TerminatorByte))
                {
                    terminatorByte = Convert.ToByte(group.Config.TerminatorByte, 16);
                }

                // Track duplicate pointers to support empty dummy strings
                Dictionary<int, string> ptrToText = new Dictionary<int, string>();
                
                foreach (var entry in group.Entries)
                {
                    if (string.IsNullOrEmpty(entry.PointerAddress)) continue;
                    
                    int pointerAddress = Convert.ToInt32(entry.PointerAddress, 16);
                    int ptrValue = romData[pointerAddress] | (romData[pointerAddress + 1] << 8);
                    int textOffset = pointerBase + ptrValue;
                    
                    if (ptrToText.ContainsKey(textOffset))
                    {
                        // Duplicate pointer detected (dummy string)
                        entry.TextOriginal = "";
                        continue;
                    }

                    string extracted = "";
                    int currentOffset = textOffset;
                    while (currentOffset < romData.Length)
                    {
                        byte b = romData[currentOffset];
                        if (b == terminatorByte)
                        {
                            extracted += $"[{b:X2}]";
                            break;
                        }
                        
                        if (parser.hexToText.ContainsKey(b)) extracted += parser.hexToText[b];
                        else if (b == 0x3C) extracted += "[3C]";
                        else if (b == 0x3D) extracted += "[3D]";
                        else extracted += $"[{b:X2}]";
                        
                        currentOffset++;
                    }
                    
                    ptrToText[textOffset] = extracted;
                    entry.TextOriginal = extracted;
                }
            }

            File.WriteAllText(jsonFile, JsonSerializer.Serialize(groups, options));
            Console.WriteLine($"{jsonFile} extracted successfully.");
        }
    }
}
