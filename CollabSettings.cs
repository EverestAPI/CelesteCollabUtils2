
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabSettings : EverestModuleSettings {
        public enum SpeedBerryTimerPositions { TopLeft, TopCenter };
        public enum BestTimeInJournal { SpeedBerry, ChapterTimer };

        [DefaultButtonBinding(Buttons.RightStick, Keys.Tab)]
        public ButtonBinding DisplayLobbyMap { get; set; }

        [DefaultButtonBinding(0, Keys.LeftShift)]
        public ButtonBinding HoldToPan { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickUp, 0)]
        public ButtonBinding PanLobbyMapUp { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickDown, 0)]
        public ButtonBinding PanLobbyMapDown { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickLeft, 0)]
        public ButtonBinding PanLobbyMapLeft { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickRight, 0)]
        public ButtonBinding PanLobbyMapRight { get; set; }

        public SpeedBerryTimerPositions SpeedBerryTimerPosition { get; set; } = SpeedBerryTimerPositions.TopLeft;

        public bool HideSpeedBerryTimerDuringGameplay { get; set; } = false;

        [SettingSubText("modoptions_collab_displayendscreenforallmaps_description")]
        public bool DisplayEndScreenForAllMaps { get; set; } = false;

        public BestTimeInJournal BestTimeToDisplayInJournal { get; set; } = BestTimeInJournal.SpeedBerry;

        public bool SaveByDefaultWhenReturningToLobby { get; set; } = true;
    }
}
