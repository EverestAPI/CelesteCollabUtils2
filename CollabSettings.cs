
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabSettings : EverestModuleSettings {
        public enum SpeedBerryTimerPositions { TopLeft, TopCenter };
        public enum MapOptions { Always, BigMapOnly, Never };
        public enum BestTimeInJournal { SpeedBerry, ChapterTimer };

        public SpeedBerryTimerPositions SpeedBerryTimerPosition { get; set; } = SpeedBerryTimerPositions.TopLeft;

        public MapOptions ShowOffscreenLevelsOnMap { get; set; } = MapOptions.BigMapOnly;

        public bool MinimapEnabled { get; set; } = false;

        public bool ColoredMinimap { get; set; } = true;

        [SettingRange(50, 150, true)]
        public int MinimapSize { get; set; } = 75;

        [SettingName("modoptions_collab_mapbind")]
        [DefaultButtonBinding(Buttons.Back, Keys.Tab)]
        public ButtonBinding MapBinding { get; set; } = new ButtonBinding(Buttons.Back, Keys.LeftShift);

        public bool HideSpeedBerryTimerDuringGameplay { get; set; } = false;

        [SettingSubText("modoptions_collab_displayendscreenforallmaps_description")]
        public bool DisplayEndScreenForAllMaps { get; set; } = false;

        public BestTimeInJournal BestTimeToDisplayInJournal { get; set; } = BestTimeInJournal.SpeedBerry;

        public bool SaveByDefaultWhenReturningToLobby { get; set; } = true;
    }
}
