using Celeste.Mod.Entities;
using Monocle;
using MonoMod.Utils;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/GoldenBerryPlayerRespawnPoint")]
    public class GoldenBerryPlayerRespawnPoint : Entity {
        public override void Added(Scene scene) {
            // only the entity data of this entity is used, not the entity itself, so we can remove it.
            base.Added(scene);
            RemoveSelf();
        }

        internal static void Load() {
            On.Celeste.Session.Restart += onSessionRestart;
        }

        internal static void Unload() {
            On.Celeste.Session.Restart -= onSessionRestart;
        }

        private static Session onSessionRestart(On.Celeste.Session.orig_Restart orig, Session self, string intoLevel) {
            Session restartSession = orig(self, intoLevel);

            if (intoLevel != null && Engine.Scene is LevelExit exit && exit.mode == LevelExit.Mode.GoldenBerryRestart) {
                // we are doing a golden berry restart! look for a golden berry player respawn point.
                LevelData levelData = restartSession.MapData.Levels.Find(level => level.Name == intoLevel);
                EntityData goldenRespawn = levelData.Entities.FirstOrDefault(entityData => entityData.Name == "CollabUtils2/GoldenBerryPlayerRespawnPoint");
                if (goldenRespawn != null) {
                    restartSession.RespawnPoint = goldenRespawn.Position + levelData.Position;
                    restartSession.StartedFromBeginning = false;
                }
            }

            return restartSession;
        }
    }
}
