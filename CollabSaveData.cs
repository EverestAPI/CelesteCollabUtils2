using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabSaveData : EverestModuleSaveData {
        // collects the SIDs of all lobbies where the mini heart door was already opened.
        public HashSet<string> OpenedMiniHeartDoors { get; set; } = new HashSet<string>();
    }
}
