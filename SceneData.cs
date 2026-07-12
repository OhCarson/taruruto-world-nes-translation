using System.Collections.Generic;

namespace TaruruutoCLI
{
    public class SceneData
    {
        public int SceneId { get; set; }
        public int PrgPointerOffset { get; set; }
        public int TargetChrBank { get; set; }
        public List<TextData> Pointers { get; set; } = new List<TextData>();
    }
}
