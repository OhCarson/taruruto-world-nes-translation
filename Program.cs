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

    // Expand chr rom (16 blocks = 128KB, bringing total CHR to 256KB)
    rom.ExpandChrRom(16);
    rom.ExpandPrgRom();

    // Apply ASM patches to redirect bank loads
    rom.romData[0x1A7D + 1] = 0x88; // Bank 0x70 -> 0x88
    rom.romData[0x3ED7 + 1] = 0x8C; // Bank 0x74 -> 0x8C
    rom.romData[0x60BB + 1] = 0x8A; // Bank 0x72 -> 0x8A
    rom.romData[0x6579 + 1] = 0x8B; // Bank 0x73 -> 0x8B
    rom.romData[0x67E4 + 1] = 0x80; // Bank 0x78 -> 0x80
    rom.romData[0x14975 + 1] = 0x8F; // Bank 0x77 -> 0x8F
    rom.romData[0x14F4D + 1] = 0x8B; // Bank 0x73 -> 0x8B
    rom.romData[0x15369 + 1] = 0x80; // Bank 0x78 -> 0x80
    rom.romData[0x153B7 + 1] = 0x81; // Bank 0x79 -> 0x81
    rom.romData[0x15826 + 1] = 0x86; // Bank 0x7E -> 0x86
    rom.romData[0x15950 + 1] = 0x85; // Bank 0x7D -> 0x85
    rom.romData[0x16581 + 1] = 0x82; // Bank 0x7A -> 0x82
    rom.romData[0x165EA + 1] = 0x8C; // Bank 0x74 -> 0x8C
    rom.romData[0x17539 + 1] = 0x8B; // Bank 0x73 -> 0x8B
    rom.romData[0x17B6A + 1] = 0x80; // Bank 0x78 -> 0x80
    rom.romData[0x17C44 + 1] = 0x80; // Bank 0x78 -> 0x80
    rom.romData[0x17C62 + 1] = 0x8C; // Bank 0x74 -> 0x8C
    rom.romData[0x17D69 + 1] = 0x8C; // Bank 0x74 -> 0x8C
    rom.romData[0x17D6E + 1] = 0x87; // Bank 0x7F -> 0x87
    rom.romData[0x17DF3 + 1] = 0x8C; // Bank 0x74 -> 0x8C


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
