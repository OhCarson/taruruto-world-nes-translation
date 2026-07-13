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

// Load table
TableParser parser80 = new TableParser();
parser80.LoadTable(tablePath);
parser80.LoadNames(namesPath);

bool isExtractionMode = args.Length > 0 && args[0].ToLower() == "extract";

if (isExtractionMode)
{
    // Extraction mode: read from Japanese ROM and update JSON OriginalText
    // In this project, Roms/mtk-eng-chr.nes works since we haven't overwritten all original data
    Console.WriteLine("Running in EXTRACTION mode...");
    RomHandler rom = new RomHandler(romPath);

    // Extract Fixed Length Text
    rom.ExtractFixedLengthText(charDataPath, parser80);
    rom.ExtractFixedLengthText(itemsPath, parser80);
    rom.ExtractFixedLengthText(menusPath, parser80);

    // Extract Dynamic Text
    rom.ExtractTextWithPointers(mapTextPath, parser80);
    rom.ExtractTextWithPointers(shopTextPath, parser80);
    rom.ExtractTextWithPointers(fileMenuPath, parser80);
    rom.ExtractTextWithPointers(preLevelEquipPath, parser80);

    Console.WriteLine("Extraction complete!");
}
else
{
    // Compilation mode: standard patching
    Console.WriteLine("Running in COMPILATION mode...");
    RomHandler rom = new RomHandler(romPath);

    // Expand chr rom
    rom.ExpandChrRom(32);
    rom.ExpandPrgRom();

    // Apply fixed and static text patches
    rom.PatchFixedLengthText(charDataPath, parser80);
    rom.PatchFixedLengthText(itemsPath, parser80);
    rom.PatchFixedLengthText(menusPath, parser80);

    // Apply dynamic text patches
    rom.PatchTextWithPointers(mapTextPath, parser80, false);
    rom.PatchTextWithPointers(shopTextPath, parser80, false);
    rom.PatchTextWithPointers(fileMenuPath, parser80, false);
    rom.PatchTextWithPointers(preLevelEquipPath, parser80, true);

    // Compile script
    rom.CompileScript(cutscenePath, parser80);

    // Hardcoded output file name
    string outputPath = "mtk-eng-full.nes";
    rom.SaveRom(outputPath);
    Console.WriteLine($"ROM Successfully Compiled to: {outputPath}");
}
