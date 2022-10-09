using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/RainbowBerryUnlockCutsceneTrigger")]
    class RainbowBerryUnlockCutsceneTrigger : Trigger {
        private RainbowBerry berry;

        private readonly string levelSet;
        private readonly string maps;

        public RainbowBerryUnlockCutsceneTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            levelSet = data.Attr("levelSet");
            maps = data.Attr("maps");
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            berry = Scene.Entities.OfType<RainbowBerry>()
                .Where(b => b.MatchesRainbowBerryTriggerWithSettings(levelSet, maps))
                .FirstOrDefault();
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            if (berry != null && berry.HologramForCutscene != null && player != null) {
                // spawn the unlock cutscene.
                Scene.Add(new RainbowBerryUnlockCutscene(berry, berry.HologramForCutscene, berry.CutsceneTotalBerries));

                // clean up the reference to the holo rainbow berry.
                berry.HologramForCutscene = null;

                // save that the cutscene happened so that it doesn't happen again.
                CollabModule.Instance.SaveData.CombinedRainbowBerries.Add(berry.GetCombinedRainbowId(Scene as Level));
            }

            // this trigger is one-use.
            RemoveSelf();
        }
    }
}
