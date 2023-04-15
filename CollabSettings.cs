
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabSettings : EverestModuleSettings {
        public enum SpeedBerryTimerPositions { TopLeft, TopCenter };
        public enum BestTimeInJournal { SpeedBerry, ChapterTimer };

        [DefaultButtonBinding(Buttons.LeftTrigger, Keys.Tab)]
        public ButtonBinding DisplayLobbyMap { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickUp, Keys.None)]
        public ButtonBinding PanLobbyMapUp { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickDown, Keys.None)]
        public ButtonBinding PanLobbyMapDown { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickLeft, Keys.None)]
        public ButtonBinding PanLobbyMapLeft { get; set; }

        [DefaultButtonBinding(Buttons.RightThumbstickRight, Keys.None)]
        public ButtonBinding PanLobbyMapRight { get; set; }

        public SpeedBerryTimerPositions SpeedBerryTimerPosition { get; set; } = SpeedBerryTimerPositions.TopLeft;

        public bool HideSpeedBerryTimerDuringGameplay { get; set; } = false;

        [SettingSubText("modoptions_collab_displayendscreenforallmaps_description")]
        public bool DisplayEndScreenForAllMaps { get; set; } = false;

        public BestTimeInJournal BestTimeToDisplayInJournal { get; set; } = BestTimeInJournal.SpeedBerry;

        public bool SaveByDefaultWhenReturningToLobby { get; set; } = true;
    }
}
