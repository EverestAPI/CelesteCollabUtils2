using Celeste.Mod.CollabUtils2.Triggers;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
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
        private static string temporaryGymExitMapSIDHolder;
        private static bool temporaryGymExitSaveAllowedHolder;
        private static bool forceInitializeModSession;

        internal static void Load() {
            On.Celeste.OuiChapterPanel.StartRoutine += modChapterPanelStartRoutine;
            Everest.Events.Level.OnCreatePauseMenuButtons += onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor += onLevelExitConstructor;
            On.Celeste.LevelLoader.ctor += onLevelLoaderConstructor;
            On.Celeste.SaveData.StartSession += onSaveDataStartSession;
            On.Celeste.LevelLoader.StartLevel += onLevelLoaderStartLevel;

            using (new DetourContext { Before = { "*" } }) {
                On.Celeste.LevelEnter.Go += onLevelEnterGo;
            }
        }

        internal static void Unload() {
            On.Celeste.OuiChapterPanel.StartRoutine -= modChapterPanelStartRoutine;
            Everest.Events.Level.OnCreatePauseMenuButtons -= onCreatePauseMenuButtons;
            On.Celeste.LevelExit.ctor -= onLevelExitConstructor;
            On.Celeste.LevelLoader.ctor -= onLevelLoaderConstructor;
            On.Celeste.SaveData.StartSession -= onSaveDataStartSession;
            On.Celeste.LevelEnter.Go -= onLevelEnterGo;
            On.Celeste.LevelLoader.StartLevel -= onLevelLoaderStartLevel;
        }

        private static IEnumerator modChapterPanelStartRoutine(On.Celeste.OuiChapterPanel.orig_StartRoutine orig, OuiChapterPanel self, string checkpoint) {
            DynData<Overworld> data = new DynData<Overworld>(self.Overworld);
            AreaData forceArea = self.Overworld == null ? null : data.Get<AreaData>("collabInGameForcedArea");

            // wait for the "chapter start" animation to finish.
            IEnumerator origRoutine = orig(self, checkpoint);
            while (origRoutine.MoveNext()) {
                yield return origRoutine.Current;

                // the last step before calling LevelEnter.Go is a yield return 0.5f.
                if (origRoutine.Current is float f && f == 0.5f && Engine.Scene is Level level) {
                    // we're exiting the lobby, so we need to make sure mods are aware we're exiting the level!
                    // calling the LevelExit constructor triggers the Level.Exit Everest event, so that makes mods less confused about what's going on.
                    new LevelExit(LevelExit.Mode.GiveUp, level.Session);
                }
            }

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

                if (LobbyHelper.IsCollabLobby((Engine.Scene as Level)?.Session?.MapData?.Area.GetSID() ?? "")) {
                    // be sure to grab the gym map SID if there is one!
                    if (data.Data.TryGetValue("gymExitMapSID", out object gymExitMapSID)) {
                        temporaryGymExitMapSIDHolder = (string) gymExitMapSID;
                        temporaryGymExitSaveAllowedHolder = data.Get<bool>("gymExitSaveAllowed");
                    }
                } else {
                    // be sure to carry over the gym map SID, especially if we're going from gym to gym.
                    temporaryGymExitMapSIDHolder = CollabModule.Instance.Session.GymExitMapSID;
                    temporaryGymExitSaveAllowedHolder = CollabModule.Instance.Session.GymExitSaveAllowed;
                }
            } else {
                // current chapter panel isn't in-game: make sure the "temporary" variables are empty.
                temporaryLobbySIDHolder = null;
                temporaryRoomHolder = null;
                temporarySpawnPointHolder = Vector2.Zero;
                temporarySaveAllowedHolder = false;
                temporaryGymExitMapSIDHolder = null;
                temporaryGymExitSaveAllowedHolder = false;
                forceInitializeModSession = false;
            }

            if (forceInitializeModSession) {
                // we want to initialize the session even when we selected "Continue", since we want to use the chapter panel settings
                // instead of whatever we had in our session last time.
                OnSessionCreated();
            }
        }

        public static void OnSessionCreated() {
            // transfer the lobby info into the session that was just created.
            CollabModule.Instance.Session.LobbySID = temporaryLobbySIDHolder;
            CollabModule.Instance.Session.LobbyRoom = temporaryRoomHolder;
            CollabModule.Instance.Session.LobbySpawnPointX = temporarySpawnPointHolder.X;
            CollabModule.Instance.Session.LobbySpawnPointY = temporarySpawnPointHolder.Y;
            CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed = temporarySaveAllowedHolder;
            CollabModule.Instance.Session.GymExitMapSID = temporaryGymExitMapSIDHolder;
            CollabModule.Instance.Session.GymExitSaveAllowed = temporaryGymExitSaveAllowedHolder;
            temporaryLobbySIDHolder = null;
            temporaryRoomHolder = null;
            temporarySpawnPointHolder = Vector2.Zero;
            temporarySaveAllowedHolder = false;
            temporaryGymExitMapSIDHolder = null;
            temporaryGymExitSaveAllowedHolder = false;
            forceInitializeModSession = false;

            if (CollabModule.Instance.Session.LobbySID == null) {
                Session session = SaveData.Instance.CurrentSession_Safe;
                string lobbySID = LobbyHelper.GetLobbyForLevelSet(session.Area.GetLevelSet());
                if (lobbySID == null) {
                    lobbySID = LobbyHelper.GetLobbyForGym(session.Area.GetSID());
                }
                if (lobbySID != null) {
                    Logger.Log(LogLevel.Warn, "CollabUtils2/ReturnToLobbyHelper", $"We are in {session.Area.GetSID()} without a Return to Lobby button! Setting it to {lobbySID}.");
                    CollabModule.Instance.Session.LobbySID = lobbySID;

                    // Try finding the chapter panel trigger of the map in order to restore the spawn point and such.
                    foreach (LevelData room in AreaData.Get(lobbySID).Mode[0].MapData.Levels) {
                        EntityData foundEntity = null;

                        // search for chapter panel triggers
                        foreach (EntityData trigger in room.Triggers) {
                            if (trigger.Name == "CollabUtils2/ChapterPanelTrigger" && trigger.Attr("map") == session.Area.GetSID()) {
                                foundEntity = trigger;
                                break;
                            }
                        }

                        if (foundEntity == null) {
                            // search for helper entities from Strawberry Jam and the FLCC collab
                            foreach (EntityData entity in room.Entities) {
                                if ((entity.Name == "SJ2021/StrawberryJamJar" || entity.Name == "FlushelineCollab/LevelEntrance")
                                    && entity.Attr("map") == session.Area.GetSID()) {

                                    foundEntity = entity;
                                    break;
                                }
                            }
                        }

                        if (foundEntity != null) {
                            // found it!
                            Vector2 spawnPoint = room.Spawns.ClosestTo(foundEntity.Position + room.Position);

                            CollabModule.Instance.Session.LobbyRoom = room.Name;
                            CollabModule.Instance.Session.LobbySpawnPointX = spawnPoint.X;
                            CollabModule.Instance.Session.LobbySpawnPointY = spawnPoint.Y;
                            CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed = foundEntity.Bool("allowSaving", defaultValue: true);
                            Logger.Log(LogLevel.Info, "CollabUtils2/ReturnToLobbyHelper", "Found respawn information: "
                                + "room = " + CollabModule.Instance.Session.LobbyRoom + ", "
                                + "spawn point = " + spawnPoint + ", "
                                + "allow saving = " + CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed);
                            break;
                        }
                    }
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

                // instantiate the "Return to Lobby" button
                TextMenu.Button returnToLobbyButton = new TextMenu.Button(Dialog.Clean("collabutils2_returntolobby"));
                returnToLobbyButton.Pressed(() => {
                    level.PauseMainMenuOpen = false;
                    menu.RemoveSelf();
                    openReturnToLobbyConfirmMenu(level, menu.Selection);
                });
                returnToLobbyButton.ConfirmSfx = "event:/ui/main/message_confirm";

                // replace the "return to map" button with "return to lobby"
                menu.Remove(menu.Items[returnToMapIndex]);
                menu.Insert(returnToMapIndex, returnToLobbyButton);
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
                temporaryGymExitMapSIDHolder = CollabModule.Instance.Session.GymExitMapSID;
                temporaryGymExitSaveAllowedHolder = CollabModule.Instance.Session.GymExitSaveAllowed;
            }
            if ((mode == LevelExit.Mode.GiveUp || mode == LevelExit.Mode.Completed) && CollabModule.Instance.Session.LobbySID != null) {
                // be sure that Return to Map and such from a collab entry returns to the lobby, not to the collab entry...
                // if the lobby exists, of course.
                AreaData lobby = AreaData.Get(CollabModule.Instance.Session.LobbySID);
                if (lobby != null) {
                    SaveData.Instance.LastArea_Safe = lobby.ToKey();
                }
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
                    Audio.BusStopAll(Buses.GAMEPLAY, immediate: true);

                    // add a death, like vanilla Save & Quit
                    level.Session.InArea = true;
                    level.Session.Deaths++;
                    level.Session.DeathsInCurrentLevel++;
                    SaveData.Instance.AddDeath(level.Session.Area);

                    level.DoScreenWipe(wipeIn: false, () => {
                        CollabModule.Instance.SaveData.SessionsPerLevel[level.Session.Area.GetSID()] = Encoding.UTF8.GetString(UserIO.Serialize(level.Session));

                        // save all mod sessions of mods that have mod sessions.
                        Dictionary<string, string> modSessions = new Dictionary<string, string>();
                        Dictionary<string, string> modSessionsBinary = new Dictionary<string, string>();
                        foreach (EverestModule mod in Everest.Modules) {
                            if (mod == CollabModule.Instance) {
                                // we do NOT want to mess with our own session!
                                continue;
                            }

                            if (mod.SaveDataAsync) {
                                // new save data API: session is serialized into a byte array.
                                byte[] sessionBinary = mod.SerializeSession(SaveData.Instance.FileSlot);
                                if (sessionBinary != null) {
                                    modSessionsBinary[mod.Metadata.Name] = Convert.ToBase64String(sessionBinary);
                                }
                            } else if (mod._Session != null && !(mod._Session is EverestModuleBinarySession)) {
                                // old behavior: serialize save data ourselves, as a string.
                                try {
                                    modSessions[mod.Metadata.Name] = YamlHelper.Serializer.Serialize(mod._Session);
                                } catch (Exception e) {
                                    // this is the same fallback message as the base EverestModule class if something goes wrong.
                                    Logger.Log(LogLevel.Warn, "CollabUtils2/ReturnToLobbyHelper", "Failed to save the session of " + mod.Metadata.Name + "!");
                                    Logger.LogDetailed(e);
                                }
                            }
                        }
                        CollabModule.Instance.SaveData.ModSessionsPerLevel[level.Session.Area.GetSID()] = modSessions;
                        CollabModule.Instance.SaveData.ModSessionsPerLevelBinary[level.Session.Area.GetSID()] = modSessionsBinary;

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
                Audio.BusStopAll(Buses.GAMEPLAY, immediate: true);

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

            if (CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed && !CollabModule.Instance.Settings.SaveByDefaultWhenReturningToLobby) {
                // select "do not save" by default
                menu.Selection = menu.FirstPossibleSelection + 1;
            }

            level.Add(menu);
        }

        private static void onLevelLoaderConstructor(On.Celeste.LevelLoader.orig_ctor orig, LevelLoader self, Session session, Vector2? startPosition) {
            if (Engine.Scene is Level level && level.Session != session && session.Area.ID == level.Session.Area.ID) {
                Logger.Log(LogLevel.Info, "CollabUtils2/ReturnToLobbyHelper", "Teleporting within the level: conserving mod session");
                temporaryLobbySIDHolder = CollabModule.Instance.Session.LobbySID;
                temporaryRoomHolder = CollabModule.Instance.Session.LobbyRoom;
                temporarySpawnPointHolder = new Vector2(CollabModule.Instance.Session.LobbySpawnPointX, CollabModule.Instance.Session.LobbySpawnPointY);
                temporarySaveAllowedHolder = CollabModule.Instance.Session.SaveAndReturnToLobbyAllowed;
                temporaryGymExitMapSIDHolder = CollabModule.Instance.Session.GymExitMapSID;
                temporaryGymExitSaveAllowedHolder = CollabModule.Instance.Session.GymExitSaveAllowed;
            }

            orig(self, session, startPosition);
        }


        private static void onLevelEnterGo(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData) {
            if (CollabModule.Instance.SaveData.SessionsPerLevel.TryGetValue(session.Area.GetSID(), out string savedSessionXML)) {
                // "save and return to lobby" was used: restore the session.
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(savedSessionXML))) {
                    session = (Session) new XmlSerializer(typeof(Session)).Deserialize(stream);
                    SaveData.Instance.CurrentSession_Safe = session;
                    fromSaveData = true;
                }

                // and remove it from the save, so that the user won't be able to use it again unless they "save and return to lobby" again.
                CollabModule.Instance.SaveData.SessionsPerLevel.Remove(session.Area.GetSID());
            }

            if (loadModSessions(session)) {
                // remove the mod sessions from the save, so that the user won't be able to use them again unless they "save and return to lobby" again.
                CollabModule.Instance.SaveData.ModSessionsPerLevel.Remove(session.Area.GetSID());
                CollabModule.Instance.SaveData.ModSessionsPerLevelBinary.Remove(session.Area.GetSID());
            }

            orig(session, fromSaveData);
        }

        private static void onSaveDataStartSession(On.Celeste.SaveData.orig_StartSession orig, SaveData self, Session session) {
            orig(self, session);

            // load any mod session here if it wasn't done before.
            if (loadModSessions(session)) {
                CollabModule.Instance.SaveData.ModSessionsPerLevel.Remove(session.Area.GetSID());
                CollabModule.Instance.SaveData.ModSessionsPerLevelBinary.Remove(session.Area.GetSID());
            }
        }

        private static bool loadModSessions(Session session) {
            if (CollabModule.Instance.SaveData.ModSessionsPerLevel.TryGetValue(session.Area.GetSID(), out Dictionary<string, string> sessions)) {
                CollabModule.Instance.SaveData.ModSessionsPerLevelBinary.TryGetValue(session.Area.GetSID(), out Dictionary<string, string> sessionsBinary);

                // restore all mod sessions we can restore.
                foreach (EverestModule mod in Everest.Modules) {
                    if (mod == CollabModule.Instance) {
                        // it is too early to load the Collab Utils mod session, but we should do that when the start routine is over.
                        forceInitializeModSession = true;
                        continue;
                    }

                    if (mod.SaveDataAsync && sessionsBinary != null && sessionsBinary.TryGetValue(mod.Metadata.Name, out string savedSessionBinary)) {
                        // new save data API: session is deserialized by passing the byte array as is.
                        mod.DeserializeSession(SaveData.Instance.FileSlot, Convert.FromBase64String(savedSessionBinary));
                    }
                    if (mod._Session != null && sessions.TryGetValue(mod.Metadata.Name, out string savedSession)) {
                        // old behavior: deserialize the session ourselves from a string.
                        try {
                            // note: we are deserializing the session rather than just storing the object, because loading the session usually does that,
                            // and a mod could react to a setter on its session being called.
                            YamlHelper.DeserializerUsing(mod._Session).Deserialize(savedSession, mod.SessionType);
                        } catch (Exception e) {
                            // this is the same fallback message as the base EverestModule class if something goes wrong.
                            Logger.Log(LogLevel.Warn, "CollabUtils2/ReturnToLobbyHelper", "Failed to load the session of " + mod.Metadata.Name + "!");
                            Logger.LogDetailed(e);
                        }
                    }
                }

                return true;
            }

            return false;
        }

        private static void onLevelLoaderStartLevel(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self) {
            // loading is finished, so we don't need those anymore, in any case.
            temporaryLobbySIDHolder = null;
            temporaryRoomHolder = null;
            temporarySpawnPointHolder = Vector2.Zero;
            temporarySaveAllowedHolder = false;
            temporaryGymExitMapSIDHolder = null;
            temporaryGymExitSaveAllowedHolder = false;
            forceInitializeModSession = false;

            orig(self);
        }
    }
}
