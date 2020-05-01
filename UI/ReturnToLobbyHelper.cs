using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.CollabUtils2.UI {
    class ReturnToLobbyHelper {
        private static string temporaryLobbySIDHolder;
        private static string temporaryRoomHolder;
        private static Vector2 temporarySpawnPointHolder;

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
                // current chapter panel is in-game: save the current map and room.
                temporaryLobbySIDHolder = (Engine.Scene as Level)?.Session?.MapData?.Area.GetSID();
                temporaryRoomHolder = (Engine.Scene as Level)?.Session?.LevelData?.Name;

                // and save the spawn point closest to the player.
                Player player = Engine.Scene.Tracker.GetEntity<Player>();
                if (player != null) {
                    temporarySpawnPointHolder = (Engine.Scene as Level).GetSpawnPoint(player.Position);
                }
            }

            orig(self, checkpoint);
        }

        public static void OnSessionCreated() {
            // transfer the lobby info into the session that was just created.
            CollabModule.Instance.Session.LobbySID = temporaryLobbySIDHolder;
            CollabModule.Instance.Session.LobbyRoom = temporaryRoomHolder;
            CollabModule.Instance.Session.LobbySpawnPointX = temporarySpawnPointHolder.X;
            CollabModule.Instance.Session.LobbySpawnPointY = temporarySpawnPointHolder.Y;
            temporaryLobbySIDHolder = null;
            temporaryRoomHolder = null;
            temporarySpawnPointHolder = Vector2.Zero;
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
                            Engine.Scene = new LevelExitToLobby(LevelExit.Mode.GiveUp, level.Session);
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
                // be sure to keep the lobby info in the session, even if we are resetting it.
                temporaryLobbySIDHolder = CollabModule.Instance.Session.LobbySID;
                temporaryRoomHolder = CollabModule.Instance.Session.LobbyRoom;
                temporarySpawnPointHolder = new Vector2(CollabModule.Instance.Session.LobbySpawnPointX, CollabModule.Instance.Session.LobbySpawnPointY);
            }
            if (CollabModule.Instance.Session.LobbySID != null) {
                // be sure that Return to Map and such from a collab entry returns to the lobby, not to the collab entry. 
                SaveData.Instance.LastArea_Safe = AreaData.Get(CollabModule.Instance.Session.LobbySID).ToKey();
            }
        }
    }
}
