using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    class LobbyHelper {
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
    }
}
