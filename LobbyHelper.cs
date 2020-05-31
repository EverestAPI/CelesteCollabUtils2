using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    class LobbyHelper {

        private static bool unpauseTimerOnNextAction = false;

        /// <summary>
        /// Returns the level set the given lobby SID is associated to, or null if the SID given is not a lobby.
        /// </summary>
        /// <param name="sid">The SID for a map</param>
        /// <returns>The level set name for this lobby, or null if the SID given is not a lobby</returns>
        public static string GetLobbyLevelSet(string sid) {
            if (sid.StartsWith("SpringCollab2020/0-Lobbies/")) {
                return "SpringCollab2020/" + sid.Substring("SpringCollab2020/0-Lobbies/".Length);
            }
            return null;
        }

        /// <summary>
        /// Returns the SID of the lobby corresponding to this level set.
        /// </summary>
        /// <param name="levelSet">The level set name</param>
        /// <returns>The SID of the lobby for this level set, or null if the given level set does not belong to a collab or has no matching lobby.</returns>
        public static string GetLobbyForLevelSet(string levelSet) {
            if (levelSet.StartsWith("SpringCollab2020/")) {
                // build the expected lobby name (SpringCollab2020/1-Beginner => SpringCollab2020/0-Lobbies/1-Beginner) and check it exists before returning it.
                string expectedLobbyName = "SpringCollab2020/0-Lobbies/" + levelSet.Substring("SpringCollab2020/".Length);
                if (AreaData.Get(expectedLobbyName) != null) {
                    return expectedLobbyName;
                }
            }
            return null;
        }

        /// <summary>
        /// Check if the given SID matches a collab heart side level.
        /// </summary>
        /// <param name="sid">The SID for a map</param>
        /// <returns>true if this is a collab heart side, false otherwise.</returns>
        public static bool IsHeartSide(string sid) {
            return sid.StartsWith("SpringCollab2020/") && sid.EndsWith("/ZZ-HeartSide");
        }

        public static void Load() {
            On.Celeste.Level.LoadLevel += onLoadLevel;
            On.Celeste.Player.Update += onPlayerUpdate;
            On.Celeste.SaveData.RegisterHeartGem += onRegisterHeartGem;
            On.Celeste.SaveData.RegisterPoemEntry += onRegisterPoemEntry;
            On.Celeste.SaveData.RegisterCompletion += onRegisterCompletion;
        }

        public static void Unload() {
            On.Celeste.Level.LoadLevel -= onLoadLevel;
            On.Celeste.Player.Update -= onPlayerUpdate;
            On.Celeste.SaveData.RegisterHeartGem -= onRegisterHeartGem;
            On.Celeste.SaveData.RegisterPoemEntry -= onRegisterPoemEntry;
            On.Celeste.SaveData.RegisterCompletion -= onRegisterCompletion;
        }

        public static void OnSessionCreated() {
            Session session = SaveData.Instance.CurrentSession_Safe;
            string levelSet = GetLobbyLevelSet(session.Area.GetSID());
            if (levelSet != null) {
                // set session flags for each completed map in the level set.
                // this will allow, for example, stylegrounds to get activated after completing a map.
                foreach (string mapName in SaveData.Instance.GetLevelSetStatsFor(levelSet).Areas
                    .Where(area => area.Modes[0].HeartGem)
                    .Select(area => area.GetSID().Substring(levelSet.Length + 1))) {

                    session.SetFlag($"CollabUtils2_MapCompleted_{mapName}");
                }
            }
        }

        private static void onLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
            orig(self, playerIntro, isFromLoader);

            DynData<Session> sessionData = new DynData<Session>(self.Session);
            if (sessionData.Data.ContainsKey("pauseTimerUntilAction") && sessionData.Get<bool>("pauseTimerUntilAction")) {
                sessionData["pauseTimerUntilAction"] = false;
                self.TimerStopped = true;
                unpauseTimerOnNextAction = true;
            }
        }

        private static void onPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
            orig(self);

            if (unpauseTimerOnNextAction && self.InControl
                && (Input.MoveX != 0 || Input.MoveY != 0 || Input.Grab.Check || Input.Jump.Check || Input.Dash.Check)) {

                self.SceneAs<Level>().TimerStopped = false;
                unpauseTimerOnNextAction = false;
            }
        }

        private static void onRegisterHeartGem(On.Celeste.SaveData.orig_RegisterHeartGem orig, SaveData self, AreaKey area) {
            orig(self, area);

            if (IsHeartSide(area.GetSID())) {
                string lobby = GetLobbyForLevelSet(area.GetLevelSet());
                if (lobby != null) {
                    // register the heart gem for the lobby as well.
                    self.RegisterHeartGem(AreaData.Get(lobby).ToKey());
                }
            }
        }

        private static bool onRegisterPoemEntry(On.Celeste.SaveData.orig_RegisterPoemEntry orig, SaveData self, string id) {
            bool result = orig(self, id);

            AreaKey currentArea = (Engine.Scene as Level)?.Session?.Area ?? AreaKey.Default;
            if (IsHeartSide(currentArea.GetSID())) {
                string lobby = GetLobbyForLevelSet(currentArea.GetLevelSet());
                if (lobby != null) {
                    // register the poem for the lobby level set as well.
                    List<string> levelSetPoem = self.GetLevelSetStatsFor(AreaData.Get(lobby).GetLevelSet()).Poem;
                    if (!levelSetPoem.Contains(id)) {
                        levelSetPoem.Add(id);
                    }
                }
            }

            return result;
        }

        private static void onRegisterCompletion(On.Celeste.SaveData.orig_RegisterCompletion orig, SaveData self, Session session) {
            orig(self, session);

            AreaKey currentArea = session.Area;
            if (IsHeartSide(currentArea.GetSID())) {
                string lobby = GetLobbyForLevelSet(currentArea.GetLevelSet());
                if (lobby != null) {
                    // completing the heart side should also complete the lobby.
                    AreaModeStats areaModeStats = SaveData.Instance.Areas_Safe[AreaData.Get(lobby).ID].Modes[0];
                    areaModeStats.Completed = true;
                }
            }
        }
    }
}
