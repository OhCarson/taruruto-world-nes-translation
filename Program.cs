using TaruruutoCLI;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

string romPath = "Roms/mtk-eng-chr.nes";
string tablePath = "Tables/mtk-e.tbl";
string jsonPath = "JsonData/script.json";

// Safety check!
if (!File.Exists(romPath) || !File.Exists(tablePath) || !File.Exists(jsonPath))
{
    Console.WriteLine("Error: One or more input files do not exist. Please check your paths.");
    return;
}
Console.WriteLine($"Starting ROM Compiler...");
Console.WriteLine($"- ROM: {romPath}");
Console.WriteLine($"- Table: {tablePath}");
Console.WriteLine($"- Script: {jsonPath}");

Dictionary<string, TableParser> parsers = new Dictionary<string, TableParser>();

TableParser parser80 = new TableParser();
parser80.LoadTable(tablePath);
string namesPath = Path.Combine(Path.GetDirectoryName(jsonPath), "names.json");
parser80.LoadNames(namesPath);
parsers["80"] = parser80;

string tableDir = Path.GetDirectoryName(tablePath);

TableParser parser40 = new TableParser();
string path40 = Path.Combine(tableDir, "magical_taruruuto_40.tbl");
if (File.Exists(path40)) { parser40.LoadTable(path40); parser40.LoadNames(namesPath); parsers["40"] = parser40; } else { parsers["40"] = parser80; }

TableParser parserC0 = new TableParser();
string pathC0 = Path.Combine(tableDir, "magical_taruruuto_items.tbl");
if (File.Exists(pathC0)) { parserC0.LoadTable(pathC0); parserC0.LoadNames(namesPath); parsers["C0"] = parserC0; } else { parsers["C0"] = parser80; }

string menusPath = Path.Combine(Path.GetDirectoryName(jsonPath), "menus.json");
List<Dictionary<string, string>> menus = null;
if (File.Exists(menusPath))
{
    string menuJson = File.ReadAllText(menusPath);
    menus = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(menuJson);
}
RomHandler rom = new RomHandler(romPath);
// rom.ExpandPrgRom(); // Expand PRG to 256KB max size

if (menus != null)
{
    rom.PatchMenus(menus, parsers);
}


string itemsPath = Path.Combine(Path.GetDirectoryName(jsonPath), "items.json");
List<ItemEntry> items = null;
if (File.Exists(itemsPath))
{
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    string itemsJson = File.ReadAllText(itemsPath);
    items = JsonSerializer.Deserialize<List<ItemEntry>>(itemsJson, options);
}

if (items != null)
{
    rom.PatchItems(items, parsers);
}

string levelTextPath = Path.Combine(Path.GetDirectoryName(jsonPath), "level_text.json");
  if (File.Exists(levelTextPath)) rom.PatchTextWithPointers(levelTextPath, parsers["80"], -1, 0x3E010, 8160);

string shopTextPath = Path.Combine(Path.GetDirectoryName(jsonPath), "shop_text.json");
if (File.Exists(shopTextPath)) rom.PatchTextWithPointers(shopTextPath, parsers["80"], -1, 0x3D810, 2051);

string fileMenuPath = Path.Combine(Path.GetDirectoryName(jsonPath), "file_menu.json");
if (File.Exists(fileMenuPath)) rom.PatchTextWithPointers(fileMenuPath, parsers["80"], -1, 0x3BC10, 1023);

string worldMapPath = Path.Combine(Path.GetDirectoryName(jsonPath), "world_map.json");
if (File.Exists(worldMapPath)) rom.PatchTextWithPointers(worldMapPath, parsers["80"], -1, 0x3AC10, 3070, true);

string charDataPath = Path.Combine(Path.GetDirectoryName(jsonPath), "char_data.json");
if (File.Exists(charDataPath)) rom.PatchFixedLengthText(charDataPath, parsers["80"], 0x3CC5A);

string extrasDataPath = Path.Combine(Path.GetDirectoryName(jsonPath), "extras_data.json");
if (File.Exists(extrasDataPath)) rom.PatchFixedLengthText(extrasDataPath, parsers["80"], 0x3D488);

string staticPatchesPath = Path.Combine(Path.GetDirectoryName(jsonPath), "static_patches.json");
if (File.Exists(staticPatchesPath)) rom.PatchStaticText(staticPatchesPath, parsers["80"]);


rom.BlankFurigana();
// rom.PatchKanjiNames();

int chrBankStart = rom.originalChrBlocks * 8;
int prgOffsetStart = 16 + (rom.originalPrgBlocks * 16384);
//rom.ExpandChrRom(32);
//rom.CompileScript(jsonPath, parser, chrBankStart, prgOffsetStart);

// Hardcoded output file name
string outputPath = "mtk-eng-full.nes";
rom.SaveRom(outputPath);
Console.WriteLine($"ROM Successfully Compiled to: {outputPath}");

