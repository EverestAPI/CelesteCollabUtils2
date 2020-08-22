using Monocle;
using MonoMod.Utils;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    // no [CustomEntity]: this is added in the map as an entity, but isn't loaded in as one in LoadLevel.
    class GoldenBerryPlayerRespawnPoint {
        public static void Load() {
            On.Celeste.Session.Restart += onSessionRestart;
        }

        public static void Unload() {
            On.Celeste.Session.Restart -= onSessionRestart;
        }

        private static Session onSessionRestart(On.Celeste.Session.orig_Restart orig, Session self, string intoLevel) {
            Session restartSession = orig(self, intoLevel);

            if (intoLevel != null && Engine.Scene is LevelExit exit && new DynData<LevelExit>(exit).Get<LevelExit.Mode>("mode") == LevelExit.Mode.GoldenBerryRestart) {
                // we are doing a golden berry restart! look for a golden berry player respawn point.
                LevelData levelData = restartSession.MapData.Levels.Find(level => level.Name == intoLevel);
                EntityData goldenRespawn = levelData.Entities.FirstOrDefault(entityData => entityData.Name == "CollabUtils2/GoldenBerryPlayerRespawnPoint");
                if (goldenRespawn != null) {
                    restartSession.RespawnPoint = goldenRespawn.Position + levelData.Position;
                }
            }

            return restartSession;
        }
    }
}
