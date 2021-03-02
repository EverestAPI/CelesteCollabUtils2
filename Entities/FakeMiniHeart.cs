using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/FakeMiniHeart")]
    class FakeMiniHeart : AbstractMiniHeart {
        private float respawnTimer;

        public FakeMiniHeart(EntityData data, Vector2 position, EntityID gid)
            : base(data, position, gid) { }

        public override void Update() {
            base.Update();

            if (respawnTimer > 0f) {
                respawnTimer -= Engine.DeltaTime;
                if (respawnTimer <= 0f) {
                    Collidable = (Visible = true);
                    scaleWiggler.Start();
                }
            }
        }

        protected override void heartBroken(Player player, Holdable holdable, Level level) {
            if (holdable != null) {
                makeDisappear(player, holdable.GetSpeed().Angle());
            } else {
                makeDisappear(player, player.Speed.Angle());
            }
        }

        private void makeDisappear(Player player, float angle) {
            if (Collidable) {
                Collidable = (Visible = false);
                respawnTimer = 3f;
                Celeste.Freeze(0.05f);
                SceneAs<Level>().Shake();
                SlashFx.Burst(Position, angle);
                player?.RefillDash();
            }
        }
    }
}
