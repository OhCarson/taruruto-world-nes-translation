using TaruruutoCLI;
using System;
using System.IO;

string romPath = "Roms/mtk-eng-chr.nes";
string tablePath = "Tables/mtk-e.tbl";

//cutscenes - Special
string namesPath = "JsonData/names.json";
string cutscenePath = "JsonData/cutscene.json";

//cutscenes - Map
string preLevelEquipPath = "JsonData/pre_level_equipment.json";
string mapTextPath = "JsonData/map_text.json";

//shop
string shopTextPath = "JsonData/shop_text.json";

//excyclopedia data
string charDataPath = "JsonData/char_data.json";
string itemsPath = "JsonData/items.json";

//general menus
string fileMenuPath = "JsonData/file_menu.json";
string menusPath = "JsonData/menus.json";
string staticPatchesPath = "JsonData/static_patches.json";


// Load table
TableParser parser80 = new TableParser();
parser80.LoadTable(tablePath);
parser80.LoadNames(namesPath);

// Set up rom
RomHandler rom = new RomHandler(romPath);
// rom.ExpandPrgRom(); // Expand PRG to 256KB max size

// Expand chr rom
rom.ExpandChrRom(32);

// Apply fixed and static text patches
if (File.Exists(charDataPath)) rom.PatchFixedLengthText(charDataPath, parser80);
if (File.Exists(staticPatchesPath)) rom.PatchFixedLengthText(staticPatchesPath, parser80);
if (File.Exists(itemsPath)) rom.PatchFixedLengthText(itemsPath, parser80);
if (File.Exists(menusPath)) rom.PatchFixedLengthText(menusPath, parser80);

// Apply dynamic text patches
if (File.Exists(mapTextPath)) rom.PatchTextWithPointers(mapTextPath, parser80, false);
if (File.Exists(shopTextPath)) rom.PatchTextWithPointers(shopTextPath, parser80, false);
if (File.Exists(fileMenuPath)) rom.PatchTextWithPointers(fileMenuPath, parser80, false);
if (File.Exists(preLevelEquipPath)) rom.PatchTextWithPointers(preLevelEquipPath, parser80, true);

// Compile script
if (File.Exists(cutscenePath)) rom.CompileScript(cutscenePath, parser80);

// Hardcoded output file name
string outputPath = "mtk-eng-full.nes";
rom.SaveRom(outputPath);
Console.WriteLine($"ROM Successfully Compiled to: {outputPath}");
