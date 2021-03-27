using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabSaveData : EverestModuleSaveData {
        // collects the SIDs of all lobbies where the mini heart door was already opened.
        public HashSet<string> OpenedMiniHeartDoors { get; set; } = new HashSet<string>();

        // collects the SIDs of all lobbies where the rainbow berry already formed.
        public HashSet<string> CombinedRainbowBerries { get; set; } = new HashSet<string>();

        // PBs for the speed berries, by map SID
        public Dictionary<string, long> SpeedBerryPBs { get; set; } = new Dictionary<string, long>();

        // flag used to show the "you can change the speed berry timer position in Mod Options" message only once per save
        public bool SpeedberryOptionMessageShown { get; set; } = false;

        // sessions saved when using "save and return to lobby", in XML or YAML format
        public Dictionary<string, string> SessionsPerLevel = new Dictionary<string, string>();
        public Dictionary<string, Dictionary<string, string>> ModSessionsPerLevel = new Dictionary<string, Dictionary<string, string>>();
    }
}
