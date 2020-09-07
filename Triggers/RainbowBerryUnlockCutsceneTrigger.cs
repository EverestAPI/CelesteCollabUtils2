using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/RainbowBerryUnlockCutsceneTrigger")]
    class RainbowBerryUnlockCutsceneTrigger : Trigger {
        public RainbowBerryUnlockCutsceneTrigger(EntityData data, Vector2 offset) : base(data, offset) { }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            RainbowBerry berry = Scene.Entities.OfType<RainbowBerry>().FirstOrDefault();
            if (berry != null && berry.HologramForCutscene != null && player != null) {
                // spawn the unlock cutscene.
                Scene.Add(new RainbowBerryUnlockCutscene(berry, berry.HologramForCutscene, berry.CutsceneTotalBerries));

                // clean up the reference to the holo rainbow berry.
                berry.HologramForCutscene = null;

                // save that the cutscene happened so that it doesn't happen again.
                CollabModule.Instance.SaveData.CombinedRainbowBerries.Add((Scene as Level).Session.Area.GetSID());
            }

            // this trigger is one-use.
            RemoveSelf();
        }
    }
}
