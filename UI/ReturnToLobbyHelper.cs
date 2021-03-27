using Celeste.Mod.CollabUtils2.Triggers;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Celeste.Mod.CollabUtils2.UI {
    class ReturnToLobbyHelper {
        private static string temporaryLobbySIDHolder;
        private static string temporaryRoomHolder;
        private static Vector2 temporarySpawnPointHolder;
        private static bool temporarySaveAllowedHolder;

        public static void Load() {
            On.Celeste.OuiChapterPanel.StartRoutine += modChapterPanelStartRoutine;
            Everest.Events.Level.OnCreatePauseMenuButtons += onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor += onLevelExitConstructor;
            On.Celeste.LevelLoader.ctor += onLevelLoaderConstructor;
            On.Celeste.SaveData.StartSession += onSaveDataStartSession;

            using (new DetourContext { Before = { "*" } }) {
                On.Celeste.LevelEnter.Go += onLevelEnterGo;
            }
        }

        public static void Unload() {
            On.Celeste.OuiChapterPanel.StartRoutine -= modChapterPanelStartRoutine;
            Everest.Events.Level.OnCreatePauseMenuButtons -= onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor -= onLevelExitConstructor;
            On.Celeste.LevelLoader.ctor -= onLevelLoaderConstructor;
            On.Celeste.SaveData.StartSession -= onSaveDataStartSession;
            On.Celeste.LevelEnter.Go -= onLevelEnterGo;
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

                temporarySaveAllowedHolder = data.Get<bool>("saveAndReturnToLobbyAllowed");

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
                    } else {
                        // player is dead, presumably? AAAAA
                        // let's use camera position instead.
                        temporarySpawnPointHolder = (Engine.Scene as Level).GetSpawnPoint((Engine.Scene as Level).Camera.Position + new Vector2(320 / 2, 180 / 2));
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
            CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed = temporarySaveAllowedHolder;
            temporaryLobbySIDHolder = null;
            temporaryRoomHolder = null;
            temporarySpawnPointHolder = Vector2.Zero;
            temporarySaveAllowedHolder = false;

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
                // find the position just under "Return to Map".
                int returnToMapIndex = menu.GetItems().FindIndex(item =>
                    item.GetType() == typeof(TextMenu.Button) && ((TextMenu.Button) item).Label == Dialog.Clean("MENU_PAUSE_RETURN"));

                if (returnToMapIndex == -1) {
                    // fall back to the bottom of the menu.
                    returnToMapIndex = menu.GetItems().Count - 1;
                }

                // add the "Return to Lobby" button
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
                temporarySaveAllowedHolder = CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed;
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

            // RETURN TO LOBBY?
            menu.Add(new TextMenu.Header(Dialog.Clean("collabutils2_returntolobby_confirm_title")));

            // Save
            if (CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed) {
                // add some explanatory text on the "Save" and "Do Not Save" options
                menu.Add(new TextMenu.SubHeader(Dialog.Clean("collabutils2_returntolobby_confirm_note1")));
                menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("collabutils2_returntolobby_confirm_note2")) { HeightExtra = 0f });
                menu.Add(new TextMenu.SubHeader(""));

                menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby_confirm_save")).Pressed(() => {
                    Engine.TimeRate = 1f;
                    menu.Focused = false;
                    Audio.SetMusic(null);
                    Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);

                    // add a death, like vanilla Save & Quit
                    level.Session.InArea = true;
                    level.Session.Deaths++;
                    level.Session.DeathsInCurrentLevel++;
                    SaveData.Instance.AddDeath(level.Session.Area);

                    level.DoScreenWipe(wipeIn: false, () => {
                        CollabModule.Instance.SaveData.SessionsPerLevel.Add(level.Session.Area.GetSID(), Encoding.UTF8.GetString(UserIO.Serialize(level.Session)));

                        // save all mod sessions of mods that have mod sessions.
                        Dictionary<string, string> modSessions = new Dictionary<string, string>();
                        foreach (EverestModule mod in Everest.Modules) {
                            if (mod._Session != null && !(mod._Session is EverestModuleBinarySession)) {
                                modSessions[mod.Metadata.Name] = YamlHelper.Serializer.Serialize(mod._Session);
                            }
                        }
                        CollabModule.Instance.SaveData.ModSessionsPerLevel.Add(level.Session.Area.GetSID(), modSessions);

                        Engine.Scene = new LevelExitToLobby(LevelExit.Mode.SaveAndQuit, level.Session);
                    });

                    foreach (LevelEndingHook component in level.Tracker.GetComponents<LevelEndingHook>()) {
                        component.OnEnd?.Invoke();
                    }
                }));
            }

            // Do Not Save
            menu.Add(new TextMenu.Button(Dialog.Clean(CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed ? "collabutils2_returntolobby_confirm_donotsave" : "menu_return_continue")).Pressed(() => {
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

            // Cancel
            menu.Add(new TextMenu.Button(Dialog.Clean(CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed ? "collabutils2_returntolobby_confirm_cancel" : "menu_return_cancel")).Pressed(() => {
                menu.OnCancel();
            }));

            // handle Pause button
            menu.OnPause = (menu.OnESC = () => {
                menu.RemoveSelf();
                level.Paused = false;
                Engine.FreezeTimer = 0.15f;
                Audio.Play("event:/ui/game/unpause");
            });

            // handle Cancel button
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
                temporarySaveAllowedHolder = CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed;
            }

            orig(self, session, startPosition);
        }


        private static void onLevelEnterGo(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData) {
            if (CollabModule.Instance.SaveData.SessionsPerLevel.TryGetValue(session.Area.GetSID(), out string savedSessionXML)) {
                // "save and return to lobby" was used: restore the session.
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(savedSessionXML))) {
                    session = (Session) new XmlSerializer(typeof(Session)).Deserialize(stream);
                    fromSaveData = true;
                }

                // and remove it from the save, so that the user won't be able to use it again unless they "save and return to lobby" again.
                CollabModule.Instance.SaveData.SessionsPerLevel.Remove(session.Area.GetSID());
            }

            // the mod sessions are loaded in SaveData.StartSession, but load them ahead of time here too for compatibility.
            loadModSessions(session);

            orig(session, fromSaveData);
        }

        private static void onSaveDataStartSession(On.Celeste.SaveData.orig_StartSession orig, SaveData self, Session session) {
            orig(self, session);

            if (loadModSessions(session)) {
                // remove the mod sessions from the save, so that the user won't be able to use them again unless they "save and return to lobby" again.
                CollabModule.Instance.SaveData.ModSessionsPerLevel.Remove(session.Area.GetSID());
            }
        }

        private static bool loadModSessions(Session session) {
            if (CollabModule.Instance.SaveData.ModSessionsPerLevel.TryGetValue(session.Area.GetSID(), out Dictionary<string, string> sessions)) {
                // restore all mod sessions we can restore.
                foreach (EverestModule mod in Everest.Modules) {
                    if (mod._Session != null && sessions.TryGetValue(mod.Metadata.Name, out string savedSession)) {
                        // note: we are deserializing the session rather than just storing the object, because loading the session usually does that,
                        // and a mod could react to a setter on its session being called.
                        YamlHelper.DeserializerUsing(mod._Session).Deserialize(savedSession, mod.SessionType);
                    }
                }

                return true;
            }

            return false;
        }
    }
}
