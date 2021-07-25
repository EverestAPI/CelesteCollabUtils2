using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/SpeedBerryCollectTrigger")]
    [Tracked]
    public class SpeedBerryCollectTrigger : Trigger {
        public SpeedBerryCollectTrigger(EntityData data, Vector2 offset) : base(data, offset) { }
    }
}
