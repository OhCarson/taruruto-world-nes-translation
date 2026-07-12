namespace TaruruutoCLI
{
    public class UniversalTextEntry
    {
        public string TextOriginal { get; set; }
        public string TextTranslation { get; set; }
        public int MaxLength { get; set; }
        public string Address { get; set; }
    }

    public class DynamicTextGroup
    {
        public DynamicTextConfig Config { get; set; }
        public System.Collections.Generic.List<DynamicTextEntry> Entries { get; set; }
    }

    public class DynamicTextConfig
    {
        public string TextOffset { get; set; }
        public string PointerBase { get; set; }
        public int MaxSize { get; set; }
        public string PrgPointerOffset { get; set; }
        public int TargetChrBank { get; set; }
    }

    public class DynamicTextEntry
    {
        public string TextOriginal { get; set; }
        public string TextTranslation { get; set; }
        public string PointerAddress { get; set; }
    }
}
