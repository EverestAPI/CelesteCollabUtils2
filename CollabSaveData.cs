using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabSaveData : EverestModuleSaveData {
        // collects the SIDs of all lobbies where the mini heart door was already opened.
        public HashSet<string> OpenedMiniHeartDoors { get; set; } = new HashSet<string>();

        // PBs for the speed berries, by map SID
        public Dictionary<string, long> SpeedBerryPBs { get; set; } = new Dictionary<string, long>();
    }
}
