using MonoMod.Utils;
using System;
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

        public static void Load() {
            On.Celeste.Level.LoadLevel += onLoadLevel;
            On.Celeste.Player.Update += onPlayerUpdate;
        }

        public static void Unload() {
            On.Celeste.Level.LoadLevel -= onLoadLevel;
            On.Celeste.Player.Update -= onPlayerUpdate;
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
    }
}
