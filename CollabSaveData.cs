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

        // sessions saved when using "save and return to lobby"
        // - vanilla session, in XML format (all annotations are set up to (de)serialize properly into XML)
        public Dictionary<string, string> SessionsPerLevel = new Dictionary<string, string>();
        // - mod sessions saved before collab utils 1.4.8, or for mods that don't support the save data async API (serialized in YAML format)
        public Dictionary<string, Dictionary<string, string>> ModSessionsPerLevel = new Dictionary<string, Dictionary<string, string>>();
        // - mod sessions for mods that support the save data async API (using DeserializeSession / SerializeSession)
        // in binary format, converted to base64 for more efficient saving (instead of a byte[] that gets serialized to a list of numbers)
        public Dictionary<string, Dictionary<string, string>> ModSessionsPerLevelBinary = new Dictionary<string, Dictionary<string, string>>();

        // maps lobby SIDs against a base64 encoded list of visited points, managed by LobbyVisitManager
        public Dictionary<string, string> VisitedLobbyPositions = new Dictionary<string, string>();

        // whether the map should be forced visible, regardless of exploration
        public bool RevealMap { get; set; }
        // whether the visit manager should pause visiting points, useful for TAS routing
        public bool PauseVisitingPoints { get; set; }
        // whether the lobby map controller should show visited points, useful for TAS routing
        public bool ShowVisitedPoints { get; set; }
    }
}
