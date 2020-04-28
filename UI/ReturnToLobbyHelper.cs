using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.CollabUtils2.UI {
    class ReturnToLobbyHelper {
        private static string temporaryLobbySIDHolder;

        public static void Load() {
            On.Celeste.OuiChapterPanel.Start += modChapterPanelStart;
            Everest.Events.Level.OnCreatePauseMenuButtons += onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor += onLevelExitConstructor;
        }

        public static void Unload() {
            On.Celeste.OuiChapterPanel.Start -= modChapterPanelStart;
            Everest.Events.Level.OnCreatePauseMenuButtons -= onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor -= onLevelExitConstructor;
        }

        private static void modChapterPanelStart(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self, string checkpoint) {
            AreaData forceArea = self.Overworld == null ? null : new DynamicData(self.Overworld).Get<AreaData>("collabInGameForcedArea");
            if (forceArea != null) {
                // current chapter panel is in-game: save the current map in the temporaryLobbyNameHolder variable.
                temporaryLobbySIDHolder = (Engine.Scene as Level)?.Session?.MapData?.Area.GetSID();
            }

            orig(self, checkpoint);
        }

        public static void OnSessionCreated() {
            // transfer the lobby SID into the session that was just created.
            CollabModule.Instance.Session.LobbySID = temporaryLobbySIDHolder;
            temporaryLobbySIDHolder = null;
        }

        private static void onCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
            if (CollabModule.Instance.Session.LobbySID != null) {
                int returnToMapIndex = menu.GetItems().FindIndex(item =>
                    item.GetType() == typeof(TextMenu.Button) && ((TextMenu.Button) item).Label == Dialog.Clean("MENU_PAUSE_RETURN"));

                menu.Insert(returnToMapIndex, new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby"))
                    .Pressed(() => {
                        Engine.TimeRate = 1f;
                        menu.Focused = false;
                        Audio.SetMusic(null);
                        Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);

                        level.DoScreenWipe(wipeIn: false, () => {
                            // restart chapter... into the lobby
                            level.Session.Area = AreaData.Get(CollabModule.Instance.Session.LobbySID).ToKey();
                            level.Session.Level = level.Session.MapData.StartLevel().Name;
                            Engine.Scene = new LevelExit(LevelExit.Mode.Restart, level.Session);

                            // wipe the lobby SID, we're going to the lobby
                            temporaryLobbySIDHolder = null;
                        });

                        foreach (LevelEndingHook component in level.Tracker.GetComponents<LevelEndingHook>()) {
                            component.OnEnd?.Invoke();
                        }
                    }));
            }
        }

        private static void onLevelExitConstructor(On.Celeste.LevelExit.orig_ctor orig, LevelExit self, LevelExit.Mode mode, Session session, HiresSnow snow) {
            orig(self, mode, session, snow);

            if (mode == LevelExit.Mode.Restart || mode == LevelExit.Mode.GoldenBerryRestart) {
                // be sure to keep the lobby SID in the session, even if we are resetting it.
                temporaryLobbySIDHolder = CollabModule.Instance.Session.LobbySID;
            }
            if (CollabModule.Instance.Session.LobbySID != null) {
                // be sure that Return to Map and such from a collab entry returns to the lobby, not to the collab entry. 
                SaveData.Instance.LastArea_Safe = AreaData.Get(CollabModule.Instance.Session.LobbySID).ToKey();
            }
        }
    }
}
