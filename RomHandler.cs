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
            if (currentPrgBlocks >= 16) return; // Mapper 16 max size is 256KB (16 banks)
            int extraBlocks = 16 - currentPrgBlocks;

            int currentChrBlocks = romData[5];
            int currentPrgSize = currentPrgBlocks * 16384;
            int currentChrSize = currentChrBlocks * 8192;

            byte[] newRom = new byte[16 + currentPrgSize + (extraBlocks * 16384) + currentChrSize];

            // Copy Header
            Array.Copy(romData, 0, newRom, 0, 16);

            // Copy PRG except last 16KB
            Array.Copy(romData, 16, newRom, 16, currentPrgSize - 16384);

            // Fill extra blocks with 0xFF
            for (int i = 0; i < extraBlocks * 16384; i++)
            {
                newRom[16 + currentPrgSize - 16384 + i] = 0xFF;
            }

            // Copy original last 16KB of PRG to the end of the new PRG segment
            Array.Copy(romData, 16 + currentPrgSize - 16384, newRom, 16 + currentPrgSize - 16384 + (extraBlocks * 16384), 16384);

            // Copy CHR to the very end
            Array.Copy(romData, 16 + currentPrgSize, newRom, 16 + currentPrgSize + (extraBlocks * 16384), currentChrSize);

            // Update PRG count in header
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

            // Copy header + PRG + CHR
            Array.Copy(romData, 0, newRom, 0, 16 + currentPrgSize + currentChrSize);

            // Fill extra CHR blocks with 0x00
            for (int i = 0; i < extraBlocks * 8192; i++)
            {
                newRom[16 + currentPrgSize + currentChrSize + i] = 0x00;
            }

            // Update CHR count in header
            newRom[5] = (byte)(currentChrBlocks + extraBlocks);

            this.romData = newRom;
        }

        public void CompileScript(string jsonPath, TableParser parser, int chrBankStart, int prgOffsetStart)
        {
            var options = new JsonSerializerOptions {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            string json = File.ReadAllText(jsonPath);
            List<SceneData> scenes = JsonSerializer.Deserialize<List<SceneData>>(json, options);

            foreach (var scene in scenes)
            {
                // Redirect the PRG pointer for this Scene
                romData[scene.PrgPointerOffset] = (byte)scene.TargetChrBank;

                int newBankOffset = 16 + (this.originalPrgBlocks * 16384) + (scene.TargetChrBank * 1024);
                int currentOffset = newBankOffset + (scene.Pointers.Count * 2); // Text starts after the pointer table

                for (int i = 0; i < scene.Pointers.Count; i++)
                {
                    var textData = scene.Pointers[i];
                    
                    // Update Pointer Table
                    int ptrLocation = newBankOffset + (i * 2);
                    int ptrValue = currentOffset - newBankOffset;
                    romData[ptrLocation] = (byte)(ptrValue & 0xFF);
                    romData[ptrLocation + 1] = (byte)((ptrValue >> 8) & 0xFF);

                    // Inject Text
                    int injectedBytes = InjectText(currentOffset, textData.Text, parser, 0, -100);
                    Console.WriteLine($"Injected Scene {scene.SceneId} Pointer {i} at ROM Offset {currentOffset}. Length: {injectedBytes} bytes.");
                    
                    currentOffset += injectedBytes;
                }

                int totalBytes = currentOffset - newBankOffset;
                Console.WriteLine($"\nTotal Text Length for Scene {scene.SceneId}: {totalBytes}/4096 bytes used starting at Bank {scene.TargetChrBank}.");
            }
        }

        public string ExtractText(int startOffset, TableParser parser)
        {
            var endTextBytes = new byte[] { 0xFF, 0x00 };
            var extracted = "";
            int offset = startOffset;
            while (offset < this.romData.Length)
            {
                byte b = this.romData[offset];
                if (endTextBytes.Contains(b))
                    break;
                if (parser.hexToText.ContainsKey(b))
                {
                    extracted += parser.hexToText[b];
                }
                else
                {
                    extracted += $"[{b:X2}]";
                }
                offset++;
            }
            return extracted;
        }

        public void SaveRom(string filename)
        {
            File.WriteAllBytes(filename, this.romData);
        }

        public void PatchMenus(List<Dictionary<string, string>> menus, Dictionary<string, TableParser> parsers)
        {
            foreach (var menu in menus)
            {
                int offset = Convert.ToInt32(menu["Offset"], 16);
                string text = menu["Text"];
                string tableKey = menu.ContainsKey("Table") ? menu["Table"] : "80";
                TableParser parser = parsers.ContainsKey(tableKey) ? parsers[tableKey] : parsers["80"];
                
                List<string> tokens = new List<string>();
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '[')
                    {
                        int endBracket = text.IndexOf(']', i);
                        if (endBracket != -1)
                        {
                            tokens.Add(text.Substring(i, endBracket - i + 1));
                            i = endBracket;
                            continue;
                        }
                    }
                    tokens.Add(text[i].ToString());
                }

                foreach (string token in tokens)
                {
                    if (parser.namesToHex.ContainsKey(token))
                    {
                        byte[] bytes = parser.namesToHex[token];
                        foreach (byte b in bytes) this.romData[offset++] = b;
                    }
                    else if (parser.textToHex.ContainsKey(token))
                    {
                        this.romData[offset++] = parser.textToHex[token];
                    }
                    else if (token.StartsWith("[") && token.EndsWith("]") && token.Length == 4)
                    {
                        string hex = token.Substring(1, 2);
                        this.romData[offset++] = Convert.ToByte(hex, 16);
                    }
                }
            }
        }


        public int InjectText(int startOffset, string text, TableParser parser, int preserveHeaderBytes, int startingPointerIndex = -1)
        {
            int offset = startOffset;
            int newBankOffset = startOffset - preserveHeaderBytes;
            int currentPointerIndex = startingPointerIndex;
            byte newLineByte = parser.textToHex.ContainsKey("[NL]") ? parser.textToHex["[NL]"] : (byte)0x0E;

            List<string> tokens = new List<string>();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    int endBracket = text.IndexOf(']', i);
                    if (endBracket != -1)
                    {
                        tokens.Add(text.Substring(i, endBracket - i + 1));
                        i = endBracket;
                        continue;
                    }
                }
                tokens.Add(text[i].ToString());
            }

            int currentLineLength = 0;
            int currentLineCount = 0;

            if (currentPointerIndex >= 0)
            {
                int ptrLocation = newBankOffset + (currentPointerIndex * 2);
                int ptrValue = offset - newBankOffset;
                this.romData[ptrLocation] = (byte)(ptrValue & 0xFF);
                this.romData[ptrLocation + 1] = (byte)((ptrValue >> 8) & 0xFF);
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];

                if (token == "[PTR]")
                {
                    if (currentPointerIndex >= 0)
                    {
                        currentPointerIndex++;
                        int ptrLocation = newBankOffset + (currentPointerIndex * 2);
                        int ptrValue = offset - newBankOffset;
                        this.romData[ptrLocation] = (byte)(ptrValue & 0xFF);
                        this.romData[ptrLocation + 1] = (byte)((ptrValue >> 8) & 0xFF);
                    }
                    continue;
                }

                if (token == "[NL]")
                {
                    this.romData[offset++] = newLineByte;
                    currentLineLength = 0;
                    currentLineCount++;
                    continue;
                }

                if (token == "[PAGE]" || token == "[END_BOX]")
                {
                    if (parser.textToHex.ContainsKey(token))
                    {
                        this.romData[offset++] = parser.textToHex[token];
                    }
                    currentLineLength = 0;
                    currentLineCount = 0;
                    continue;
                }

                int maxLineLength = 24;

                int wordLength = 0;
                int j = i;
                while (j < tokens.Count && tokens[j] != " " && tokens[j] != "[NL]" && tokens[j] != "[PAGE]" && tokens[j] != "[END_BOX]" && tokens[j] != "[PTR]")
                {
                    if (parser.namesToHex.ContainsKey(tokens[j]))
                    {
                        wordLength += parser.namesToHex[tokens[j]].Length;
                    }
                    else if (!tokens[j].StartsWith("["))
                    {
                        wordLength++;
                    }
                    j++;
                }

                if (currentLineLength + wordLength > maxLineLength && currentLineLength > 0 && token != " ")
                {
                    this.romData[offset++] = newLineByte;
                    currentLineLength = 0;
                    currentLineCount++;
                }

                if (token == " " && currentLineLength == 0)
                {
                    continue;
                }

                if (parser.namesToHex.ContainsKey(token))
                {
                    byte[] bytes = parser.namesToHex[token];
                    foreach (byte b in bytes)
                    {
                        this.romData[offset++] = b;
                    }
                    currentLineLength += bytes.Length;
                }
                else if (parser.textToHex.ContainsKey(token))
                {
                    this.romData[offset++] = parser.textToHex[token];
                    if (!token.StartsWith("[")) currentLineLength++;
                }
                else if (token.StartsWith("[") && token.EndsWith("]") && token.Length == 4)
                {
                    string hex = token.Substring(1, 2);
                    this.romData[offset++] = Convert.ToByte(hex, 16);
                }
            }

            this.romData[offset++] = 0x00;

            return offset - startOffset;
        }

        public void PatchItems(List<ItemEntry> items, Dictionary<string, TableParser> parsers)
        {
            foreach (var item in items)
            {
                int offset = Convert.ToInt32(item.Offset, 16);
                int maxLength = item.MaxLength;
                string text = item.Text;
                string tableKey = "C0"; // Default for items if not specified
                // Note: If ItemEntry had a Table property, we'd use it here.
                TableParser parser = parsers.ContainsKey(tableKey) ? parsers[tableKey] : parsers["C0"];
                
                List<string> tokens = new List<string>();
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '[')
                    {
                        int endBracket = text.IndexOf(']', i);
                        if (endBracket != -1)
                        {
                            tokens.Add(text.Substring(i, endBracket - i + 1));
                            i = endBracket;
                            continue;
                        }
                    }
                    tokens.Add(text[i].ToString());
                }

                List<byte> injectedBytes = new List<byte>();
                foreach (string token in tokens)
                {
                    if (parser.namesToHex.ContainsKey(token))
                    {
                        injectedBytes.AddRange(parser.namesToHex[token]);
                    }
                    else if (parser.textToHex.ContainsKey(token))
                    {
                        injectedBytes.Add(parser.textToHex[token]);
                    }
                    else if (token.StartsWith("[") && token.EndsWith("]") && token.Length == 4)
                    {
                        string hex = token.Substring(1, 2);
                        injectedBytes.Add(Convert.ToByte(hex, 16));
                    }
                }

                if (injectedBytes.Count > maxLength)
                {
                    Console.WriteLine($"ERROR: Item at {item.Offset} is {injectedBytes.Count - maxLength} bytes too long!");
                    continue; // Skip injecting if too long
                }

                for (int i = 0; i < injectedBytes.Count; i++)
                {
                    this.romData[offset + i] = injectedBytes[i];
                }
                for (int i = injectedBytes.Count; i < maxLength; i++)
                {
                    this.romData[offset + i] = 0x00;
                }
            }
        }


        public void PatchTextWithPointers(string jsonFile, TableParser parser, int textOffset, int pointerOffset, int maxSize, bool deduplicate = false)
        {
            string json = File.ReadAllText(jsonFile);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<TextEntry> texts = JsonSerializer.Deserialize<List<TextEntry>>(json, options);

            int currentTextOffset = textOffset == -1 ? pointerOffset + (texts.Count * 2) : textOffset;
            int currentPointerOffset = pointerOffset;

            Dictionary<string, int> stringToOffset = new Dictionary<string, int>();

            foreach (var entry in texts)
            {
                if (deduplicate && stringToOffset.ContainsKey(entry.Text))
                {
                    int ptrValue = stringToOffset[entry.Text] - pointerOffset;
                    romData[currentPointerOffset] = (byte)(ptrValue & 0xFF);
                    romData[currentPointerOffset + 1] = (byte)((ptrValue >> 8) & 0xFF);
                    currentPointerOffset += 2;
                }
                else
                {
                    int ptrValue = currentTextOffset - pointerOffset;
                    romData[currentPointerOffset] = (byte)(ptrValue & 0xFF);
                    romData[currentPointerOffset + 1] = (byte)((ptrValue >> 8) & 0xFF);
                    currentPointerOffset += 2;

                    if (deduplicate)
                    {
                        stringToOffset[entry.Text] = currentTextOffset;
                    }

                    List<byte> encoded = EncodeString(entry.Text, parser);
                    foreach (byte b in encoded)
                    {
                        romData[currentTextOffset++] = b;
                    }
                }
            }

            int totalSize = currentTextOffset - pointerOffset;
            if (totalSize > maxSize)
            {
                Console.WriteLine($"ERROR: {jsonFile} text size {totalSize} exceeds max size {maxSize}!");
            }
            else
            {
                Console.WriteLine($"{jsonFile} patched successfully. {totalSize}/{maxSize} bytes used.");
            }
        }

        public void PatchStaticText(string jsonFile, TableParser parser)
        {
            string json = File.ReadAllText(jsonFile);
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<StaticPatchEntry> texts = System.Text.Json.JsonSerializer.Deserialize<List<StaticPatchEntry>>(json, options);

            foreach (var entry in texts)
            {
                int currentOffset = Convert.ToInt32(entry.Offset, 16);
                List<byte> encoded = EncodeString(entry.Text, parser);
                
                foreach (byte b in encoded)
                {
                    romData[currentOffset++] = b;
                }
            }
            Console.WriteLine($"{jsonFile} patched successfully.");
        }
        public void PatchFixedLengthText(string jsonFile, TableParser parser, int startOffset)
        {
            string json = File.ReadAllText(jsonFile);
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<TextEntry> texts = System.Text.Json.JsonSerializer.Deserialize<List<TextEntry>>(json, options);

            int currentOffset = startOffset;
            foreach (var entry in texts)
            {
                List<byte> encoded = EncodeString(entry.Text, parser);
                
                if (encoded.Count > entry.Length)
                {
                    Console.WriteLine($"ERROR: String '{entry.Text}' too long: {encoded.Count} > {entry.Length}");
                    Environment.Exit(1);
                }

                while (encoded.Count < entry.Length) encoded.Add(0x02);

                foreach (byte b in encoded)
                {
                    romData[currentOffset++] = b;
                }
            }
            Console.WriteLine($"{jsonFile} patched successfully.");
        }


        private List<byte> EncodeString(string text, TableParser parser)
        {
            List<byte> outBytes = new List<byte>();
            for (int i = 0; i < text.Length; )
            {
                if (text[i] == '[')
                {
                    int end = text.IndexOf(']', i);
                    if (end != -1)
                    {
                        string token = text.Substring(i, end - i + 1);
                        if (parser.textToHex.ContainsKey(token)) outBytes.Add(parser.textToHex[token]);
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

        public void BlankFurigana()
        {
            int start = 0x12295;
            int end = 0x123A0;
            for (int i = start; i < end; i++)
            {
                romData[i] = 0x3F;
            }
            Console.WriteLine("Furigana table blanked successfully.");
        }

        public void PatchKanjiNames()
        {
            byte[][] originalHeaders = new byte[][]
            {
                new byte[] { 0x1E, 0x12, 0x22, 0x01 }, // 0
                new byte[] { 0x1E, 0x12, 0x22, 0x01 }, // 1 (shared with 0)
                new byte[] { 0x24, 0x05, 0x00, 0x00 }, // 2
                new byte[] { 0x2B, 0x10, 0x01, 0x00 }, // 3
                new byte[] { 0x21, 0x0B, 0x02, 0x00 }, // 4
                new byte[] { 0x28, 0x01, 0x03, 0x00 }, // 5
                new byte[] { 0x26, 0x17, 0x04, 0x00 }, // 6
                new byte[] { 0x27, 0x21, 0x05, 0x00 }, // 7
                new byte[] { 0x26, 0x07, 0x06, 0x00 }, // 8
                new byte[] { 0x28, 0x18, 0x07, 0x00 }, // 9
                new byte[] { 0x27, 0x06, 0x08, 0x00 }, // 10
                new byte[] { 0x29, 0x0A, 0x09, 0x00 }, // 11
                new byte[] { 0x28, 0x14, 0x0A, 0x00 }, // 12
                new byte[] { 0x24, 0x1B, 0x0B, 0x00 }, // 13
                new byte[] { 0x24, 0x1E, 0x0C, 0x00 }, // 14
                new byte[] { 0x25, 0x1C, 0x0D, 0x00 }, // 15
                new byte[] { 0x28, 0x23, 0x21, 0x01 }, // 16
                new byte[] { 0x28, 0x02, 0x0E, 0x00 }, // 17
                new byte[] { 0x2A, 0x20, 0x0F, 0x00 }, // 18
                new byte[] { 0x22, 0x00, 0x10, 0x00 }, // 19
                new byte[] { 0x28, 0x11, 0x11, 0x00 }, // 20
                new byte[] { 0x27, 0x0E, 0x12, 0x00 }, // 21
                new byte[] { 0x25, 0x08, 0x13, 0x00 }, // 22
                new byte[] { 0x3D, 0x13, 0x24, 0x01 }, // 23
                new byte[] { 0x20, 0x09, 0x14, 0x00 }, // 24
                new byte[] { 0x21, 0x16, 0x15, 0x00 }, // 25
                new byte[] { 0x29, 0x04, 0x16, 0x00 }, // 26
                new byte[] { 0x2B, 0x1D, 0x17, 0x00 }, // 27
                new byte[] { 0x2A, 0x0F, 0x18, 0x00 }, // 28
                new byte[] { 0x21, 0x0D, 0x19, 0x00 }, // 29
                new byte[] { 0x27, 0x22, 0x1A, 0x00 }, // 30
                new byte[] { 0x2B, 0x19, 0x1B, 0x00 }, // 31
                new byte[] { 0x27, 0x46, 0x1D, 0x00 }, // 32
                new byte[] { 0x2B, 0x1A, 0x1E, 0x00 }, // 33
                new byte[] { 0x26, 0x15, 0x1F, 0x00 }  // 34
            };

            string[] translatedGroups = new string[]
            {
                "TARU HON IYO", // 0
                "TARU HON IYO", // 1
                "TSUTOMU RUI",  // 2
                "JABAO KORE",   // 3
                "TSUTO RUI IYO",// 4
                "RUI IYONA",    // 5
                "MARI IYO WASE",// 6
                "MARUE RUI IYO",// 7
                "MIMORA RAIBAA",// 8
                "NIRURU SHOUG", // 9
                "CHIZURU UGUN", // 10
                "JII INA",      // 11
                "MATSU DOB GOD",// 12
                "DOWAH ERANDO", // 13
                "RAKYURU PACH", // 14
                "GABEIRA HERM", // 15
                "ERANDO MIMORA",// 16
                "SISTERS HERB", // 17
                "CHIZURU",      // 18
                "MIMORA RAIBAA",// 19
                "TSUTOMU RUI",  // 20
                "TSUTOMU RUI",  // 21
                "IYONA",        // 22
                "TARURUUTO",    // 23
                "TSUT RUI JAB", // 24
                "JABAO KORE",   // 25
                "WASEDA MARI",  // 26
                "MARUE RIA",    // 27
                "MIMORA RAIB",  // 28
                "NIRURU SHOUG", // 29
                "CHIZURU UGUN", // 30
                "JII INA",      // 31
                "MATSU DOB",    // 32
                "KING DOWAH",   // 33
                ""              // 34
            };

            int writeCursor = 0x123E2;
            int pointerCursor = 0x1239C;
            
            int[] sharedPointers = new int[35];
            for (int i = 0; i < 35; i++) sharedPointers[i] = -1;
            sharedPointers[1] = 0; 

            int[] newPointerValues = new int[35];

            for (int i = 0; i < 35; i++)
            {
                if (sharedPointers[i] != -1)
                {
                    newPointerValues[i] = newPointerValues[sharedPointers[i]];
                    continue;
                }

                newPointerValues[i] = writeCursor - 0x12010 + 0xA000;

                byte[] header = originalHeaders[i];
                for (int h = 0; h < header.Length; h++)
                {
                    romData[writeCursor++] = header[h];
                }

                string name = translatedGroups[i];
                foreach (char c in name)
                {
                    if (c == ' ')
                    {
                        romData[writeCursor++] = 0xFC; 
                    }
                    else
                    {
                        romData[writeCursor++] = (byte)c; 
                    }
                }
                
                romData[writeCursor++] = 0x00; 
            }

            for (int i = 0; i < 35; i++)
            {
                romData[pointerCursor++] = (byte)(newPointerValues[i] & 0xFF);
                romData[pointerCursor++] = (byte)(newPointerValues[i] >> 8);
            }

            Console.WriteLine($"Kanji Strings patched! New size: {writeCursor - 0x123E2} bytes (Original was 881 bytes).");
        }

        public void PatchProfileNames()
        {
            // 1. Write the String Table to Extra Bank 0 (0x1C010)
            string[] names = new string[] {
                "TARURUUTO", "HONMARU EDOJOU", "IYONA KAWAI", "TSUTOMU HARAKO",
                "RUI IJIGAWA", "JABAO", "KOREKIYO", "ZAKENJANEEYO",
                "ZAKENJANEEZOU", "MARI OOAYA", "MR WASEDA", "MARUE EMOTO",
                "RIA KIBUKAAMO", "MIMORA", "RAIBAA", "NIRURU",
                "SHOUGUNNOSUKE", "CHIZURU EDOJOU", "UGUNJI JINGUUJI", "JII",
                "INA KAWAI", "MATSUGOROU", "HERO DOBERK", "GODDESS OF LIGHT",
                "KING DOWAH", "ERANDO", "COUNT RAKYURU", "GEN PACHURASU",
                "BEAST GABEIRA", "HERMIT", "TREASURE GUARD", "KOTAROU KAPPA",
                "KUSUDA SISTERS", "HERBERT SAIMIN"
            };

            int tableOffset = 0x1C010;
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                for (int c = 0; c < 16; c++)
                {
                    byte val = 0x00; // Blank tile padding
                    if (c < name.Length)
                    {
                        char ch = name[c];
                        if (ch >= 'A' && ch <= 'Z') val = (byte)(0x40 + (ch - 'A'));
                        else if (ch >= 'a' && ch <= 'z') val = (byte)(0x40 + (ch - 'a'));
                        else if (ch == ' ') val = 0x00;
                    }
                    romData[tableOffset++] = val;
                }
            }

            // 2. Write CustomInit to Fixed Bank Free Space (0x3FF47)
            byte[] initCode = new byte[] {
                0x48, 0x8A, 0x48, 0x98, 0x48, 0xA9, 0x24, 0x85, 0x57, 0xA5, 0x50, 0x48,
                0xA9, 0x07, 0x85, 0x50, 0x8D, 0x08, 0x80, 0xAD, 0x06, 0x02, 0xC9, 0x10,
                0x90, 0x34, 0xC9, 0x20, 0x90, 0x18, 0x29, 0x0F, 0x0A, 0x0A, 0x0A, 0x0A,
                0xAA, 0xA0, 0x00, 0xBD, 0x00, 0x82, 0x99, 0x80, 0x07, 0xE8, 0xC8, 0xC0,
                0x10, 0xD0, 0xF4, 0x4C, 0x98, 0xFF, 0x29, 0x0F, 0x0A, 0x0A, 0x0A, 0x0A,
                0xAA, 0xA0, 0x00, 0xBD, 0x00, 0x81, 0x99, 0x80, 0x07, 0xE8, 0xC8, 0xC0,
                0x10, 0xD0, 0xF4, 0x4C, 0x98, 0xFF, 0x0A, 0x0A, 0x0A, 0x0A, 0xAA, 0xA0,
                0x00, 0xBD, 0x00, 0x80, 0x99, 0x80, 0x07, 0xE8, 0xC8, 0xC0, 0x10, 0xD0,
                0xF4, 0x68, 0x85, 0x50, 0x8D, 0x08, 0x80, 0x68, 0xA8, 0x68, 0xAA, 0x68,
                0xAD, 0xDA, 0x04, 0x85, 0x0E, 0x60
            };
            Array.Copy(initCode, 0, romData, 0x3FF47, initCode.Length);

            // 3. Patch Init Hook at 0xFE0A
            romData[0xFE0A] = 0x20; // JSR
            romData[0xFE0B] = 0x37; // $37
            romData[0xFE0C] = 0xFF; // $FF
            romData[0xFE0D] = 0xEA; // NOP
            romData[0xFE0E] = 0xEA; // NOP

            // 4. Patch Drawing Loop at 0xFE88
            romData[0xFE88] = 0xB9; // LDA $077F, Y
            romData[0xFE89] = 0x7F;
            romData[0xFE8A] = 0x07;
            romData[0xFE8B] = 0x9D; // STA $0700, X
            romData[0xFE8C] = 0x00;
            romData[0xFE8D] = 0x07;
            romData[0xFE8E] = 0xEA; // NOP
        }
    }
    public class ItemEntry
    {
        public string Offset { get; set; }
        public int MaxLength { get; set; }
        public string OriginalText { get; set; }
        public string Text { get; set; }
    }

    public class TextEntry
    {
        public string Text { get; set; }
        public int Length { get; set; }
    }

    public class StaticPatchEntry
    {
        public string Offset { get; set; }
        public string Text { get; set; }
    }
}
