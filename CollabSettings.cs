
namespace Celeste.Mod.CollabUtils2 {
    public class CollabSettings : EverestModuleSettings {
        public enum SpeedBerryTimerPositions { TopLeft, TopCenter };
        public enum BestTimeInJournal { SpeedBerry, ChapterTimer };

        public ButtonBinding DisplayLobbyMap { get; set; }
        public ButtonBinding HoldToPan { get; set; }
        public ButtonBinding PanLobbyMapUp { get; set; }
        public ButtonBinding PanLobbyMapDown { get; set; }
        public ButtonBinding PanLobbyMapLeft { get; set; }
        public ButtonBinding PanLobbyMapRight { get; set; }

        public SpeedBerryTimerPositions SpeedBerryTimerPosition { get; set; } = SpeedBerryTimerPositions.TopLeft;

        public bool HideSpeedBerryTimerDuringGameplay { get; set; } = false;

        [SettingSubText("modoptions_collab_displayendscreenforallmaps_description")]
        public bool DisplayEndScreenForAllMaps { get; set; } = false;

        public BestTimeInJournal BestTimeToDisplayInJournal { get; set; } = BestTimeInJournal.SpeedBerry;

        public bool SaveByDefaultWhenReturningToLobby { get; set; } = true;
    }
}
