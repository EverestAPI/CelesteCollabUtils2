using Celeste.Mod.CollabUtils2.Triggers;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.UI {
    class ReturnToLobbyHelper {
        private static string temporaryLobbySIDHolder;
        private static string temporaryRoomHolder;
        private static Vector2 temporarySpawnPointHolder;

        public static void Load() {
            On.Celeste.OuiChapterPanel.StartRoutine += modChapterPanelStartRoutine;
            Everest.Events.Level.OnCreatePauseMenuButtons += onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor += onLevelExitConstructor;
            On.Celeste.LevelLoader.ctor += onLevelLoaderConstructor;
        }

        public static void Unload() {
            On.Celeste.OuiChapterPanel.StartRoutine -= modChapterPanelStartRoutine;
            Everest.Events.Level.OnCreatePauseMenuButtons -= onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor -= onLevelExitConstructor;
            On.Celeste.LevelLoader.ctor -= onLevelLoaderConstructor;
        }

        private static IEnumerator modChapterPanelStartRoutine(On.Celeste.OuiChapterPanel.orig_StartRoutine orig, OuiChapterPanel self, string checkpoint) {
            // wait for the "chapter start" animation to finish.
            IEnumerator origRoutine = orig(self, checkpoint);
            while (origRoutine.MoveNext()) {
                yield return origRoutine.Current;
            }

            DynData<Overworld> data = new DynData<Overworld>(self.Overworld);
            AreaData forceArea = self.Overworld == null ? null : data.Get<AreaData>("collabInGameForcedArea");
            if (forceArea != null) {
                // current chapter panel is in-game: set up Return to Lobby.
                ChapterPanelTrigger.ReturnToLobbyMode returnToLobbyMode = data.Get<ChapterPanelTrigger.ReturnToLobbyMode>("returnToLobbyMode");

                if (returnToLobbyMode == ChapterPanelTrigger.ReturnToLobbyMode.DoNotChangeReturn) {
                    // carry over current values.
                    temporaryLobbySIDHolder = CollabModule.Instance.Session.LobbySID;
                    temporaryRoomHolder = CollabModule.Instance.Session.LobbyRoom;
                    temporarySpawnPointHolder = new Vector2(CollabModule.Instance.Session.LobbySpawnPointX, CollabModule.Instance.Session.LobbySpawnPointY);
                } else if (returnToLobbyMode == ChapterPanelTrigger.ReturnToLobbyMode.SetReturnToHere) {
                    // set the values to the current map, the current room, and the nearest spawn point.
                    temporaryLobbySIDHolder = (Engine.Scene as Level)?.Session?.MapData?.Area.GetSID();
                    temporaryRoomHolder = (Engine.Scene as Level)?.Session?.LevelData?.Name;

                    // and save the spawn point closest to the player.
                    Player player = Engine.Scene.Tracker.GetEntity<Player>();
                    if (player != null) {
                        temporarySpawnPointHolder = (Engine.Scene as Level).GetSpawnPoint(player.Position);
                    }
                } else if (returnToLobbyMode == ChapterPanelTrigger.ReturnToLobbyMode.RemoveReturn) {
                    // make sure the "temporary" variables are empty.
                    temporaryLobbySIDHolder = null;
                    temporaryRoomHolder = null;
                    temporarySpawnPointHolder = Vector2.Zero;
                }
            } else {
                // current chapter panel isn't in-game: make sure the "temporary" variables are empty.
                temporaryLobbySIDHolder = null;
                temporaryRoomHolder = null;
                temporarySpawnPointHolder = Vector2.Zero;
            }
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

            if (CollabModule.Instance.Session.LobbySID == null) {
                Session session = SaveData.Instance.CurrentSession_Safe;
                string lobbySID = LobbyHelper.GetLobbyForLevelSet(session.Area.GetLevelSet());
                if (lobbySID == null) {
                    lobbySID = LobbyHelper.GetLobbyForGym(session.Area.GetSID());
                }
                if (lobbySID != null) {
                    Logger.Log(LogLevel.Warn, "CollabUtils2/ReturnToLobbyHelper", $"We are in {session.Area.GetSID()} without a Return to Lobby button! Setting it to {lobbySID}.");
                    CollabModule.Instance.Session.LobbySID = lobbySID;
                }
            }
        }

        private static void onCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
            if (CollabModule.Instance.Session.LobbySID != null) {
                int returnToMapIndex = menu.GetItems().FindIndex(item =>
                    item.GetType() == typeof(TextMenu.Button) && ((TextMenu.Button) item).Label == Dialog.Clean("MENU_PAUSE_RETURN"));

                if (returnToMapIndex == -1) {
                    // fall back to the bottom of the menu.
                    returnToMapIndex = menu.GetItems().Count - 1;
                }

                TextMenu.Button returnToLobbyButton = new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby"));
                returnToLobbyButton.Pressed(() => {
                    level.PauseMainMenuOpen = false;
                    menu.RemoveSelf();
                    openReturnToLobbyConfirmMenu(level, menu.Selection);
                });
                returnToLobbyButton.ConfirmSfx = "event:/ui/main/message_confirm";
                menu.Insert(returnToMapIndex + 1, returnToLobbyButton);
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
            if ((mode == LevelExit.Mode.GiveUp || mode == LevelExit.Mode.Completed) && CollabModule.Instance.Session.LobbySID != null) {
                // be sure that Return to Map and such from a collab entry returns to the lobby, not to the collab entry. 
                SaveData.Instance.LastArea_Safe = AreaData.Get(CollabModule.Instance.Session.LobbySID).ToKey();
            }
        }

        private static void openReturnToLobbyConfirmMenu(Level level, int returnIndex) {
            level.Paused = true;
            TextMenu menu = new TextMenu();
            menu.AutoScroll = false;
            menu.Position = new Vector2((float) Engine.Width / 2f, (float) Engine.Height / 2f - 100f);
            menu.Add(new TextMenu.Header(Dialog.Clean("collabutils2_returntolobby_confirm_title")));
            menu.Add(new TextMenu.Button(Dialog.Clean("menu_return_continue")).Pressed(() => {
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
            menu.Add(new TextMenu.Button(Dialog.Clean("menu_return_cancel")).Pressed(() => {
                menu.OnCancel();
            }));
            menu.OnPause = (menu.OnESC = () => {
                menu.RemoveSelf();
                level.Paused = false;
                Engine.FreezeTimer = 0.15f;
                Audio.Play("event:/ui/game/unpause");
            });
            menu.OnCancel = () => {
                Audio.Play("event:/ui/main/button_back");
                menu.RemoveSelf();
                level.Pause(returnIndex, minimal: false);
            };
            level.Add(menu);
        }

        private static void onLevelLoaderConstructor(On.Celeste.LevelLoader.orig_ctor orig, LevelLoader self, Session session, Vector2? startPosition) {
            if (Engine.Scene is Level level && level.Session != session && session.Area.ID == level.Session.Area.ID) {
                Logger.Log(LogLevel.Info, "CollabUtils2/ReturnToLobbyHelper", "Teleporting within the level: conserving mod session");
                temporaryLobbySIDHolder = CollabModule.Instance.Session.LobbySID;
                temporaryRoomHolder = CollabModule.Instance.Session.LobbyRoom;
                temporarySpawnPointHolder = new Vector2(CollabModule.Instance.Session.LobbySpawnPointX, CollabModule.Instance.Session.LobbySpawnPointY);
            }

            orig(self, session, startPosition);
        }
    }
}
