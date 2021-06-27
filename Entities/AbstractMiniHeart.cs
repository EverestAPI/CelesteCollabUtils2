using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.Entities {
    // This is all code in common between mini hearts and fake mini hearts.
    abstract class AbstractMiniHeart : Entity {
        protected static readonly int[] animationFrames = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

        protected Sprite sprite;
        private string spriteName;
        private bool refillDash;

        protected Wiggler scaleWiggler;

        private Vector2 moveWiggleDir;
        private Wiggler moveWiggler;

        private float bounceSfxDelay;

        protected VertexLight light;
        protected BloomPoint bloom;
        private ParticleType shineParticle;

        private HoldableCollider holdableCollider;

        public AbstractMiniHeart(EntityData data, Vector2 position, EntityID gid)
            : base(data.Position + position) {

            spriteName = data.Attr("sprite");
            refillDash = data.Bool("refillDash", defaultValue: true);

            Collider = new Hitbox(12f, 12f, -6f, -6f);

            Add(scaleWiggler = Wiggler.Create(0.5f, 4f, f => {
                sprite.Scale = Vector2.One * (1f + f * 0.3f);
            }));
            moveWiggler = Wiggler.Create(0.8f, 2f);
            moveWiggler.StartZero = true;
            Add(moveWiggler);

            Add(new PlayerCollider(onPlayer));
            Add(holdableCollider = new HoldableCollider(onHoldable));
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            AreaKey area = (scene as Level).Session.Area;

            string spritePath = "CollabUtils2/miniheart/" + spriteName + "/";
            bool alreadyCollectedInSave = SaveData.Instance.Areas_Safe[area.ID].Modes[(int) area.Mode].HeartGem;
            if (alreadyCollectedInSave) {
                // use the ghost sprite specific to the heart: instead of reading 00.png, read ghost00.png
                spritePath += "ghost";
                if (!GFX.Game.Has(spritePath + "00")) {
                    // if those sprites are missing, use the default ghost heart instead
                    spritePath = "CollabUtils2/miniheart/ghost/ghost";
                }
            }

            Add(sprite = new Sprite(GFX.Game, spritePath));
            sprite.AddLoop("idle", "", 0.1f, animationFrames);
            sprite.Play("idle");
            sprite.CenterOrigin();
            sprite.OnLoop = anim => {
                if (Visible) {
                    Audio.Play("event:/SC2020_heartShard_pulse", Position);
                    scaleWiggler.Start();
                    (Scene as Level).Displacement.AddBurst(Position + sprite.Position, 0.35f, 4f, 24f, 0.25f);
                }
            };


            Color heartColor;
            switch (spriteName) {
                case "beginner":
                default:
                    heartColor = Color.Aqua;
                    shineParticle = HeartGem.P_BlueShine;
                    break;
                case "intermediate":
                    heartColor = Color.Red;
                    shineParticle = HeartGem.P_RedShine;
                    break;
                case "advanced":
                    heartColor = Color.Gold;
                    shineParticle = HeartGem.P_GoldShine;
                    break;
                case "expert":
                    heartColor = Color.Orange;
                    shineParticle = new ParticleType(HeartGem.P_BlueShine) {
                        Color = Color.Orange
                    };
                    break;
                case "grandmaster":
                    heartColor = Color.DarkViolet;
                    shineParticle = new ParticleType(HeartGem.P_BlueShine) {
                        Color = Color.DarkViolet
                    };
                    break;
            }
            if (alreadyCollectedInSave) {
                heartColor = Color.White * 0.8f;
                shineParticle = new ParticleType(HeartGem.P_BlueShine) {
                    Color = Calc.HexToColor("7589FF")
                };
            }
            heartColor = Color.Lerp(heartColor, Color.White, 0.5f);
            Add(light = new VertexLight(heartColor, 1f, 32, 64));
            Add(bloom = new BloomPoint(0.75f, 16f));
        }

        public override void Update() {
            base.Update();
            bounceSfxDelay -= Engine.DeltaTime;
            sprite.Position = moveWiggleDir * moveWiggler.Value * -8f;

            if (Visible && Scene.OnInterval(0.1f)) {
                SceneAs<Level>().Particles.Emit(shineParticle, 1, Center + sprite.Position, Vector2.One * 4f);
            }
        }

        private void onPlayer(Player player) {
            Level level = Scene as Level;
            if (player.DashAttacking) {
                // player broke the heart
                heartBroken(player, null, level);
            } else {
                // player bounces on the heart
                int dashCount = player.Dashes;
                player.PointBounce(Center);
                if (!refillDash) {
                    player.Dashes = dashCount;
                }

                moveWiggler.Start();
                scaleWiggler.Start();
                moveWiggleDir = (Center - player.Center).SafeNormalize(Vector2.UnitY);
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
                if (bounceSfxDelay <= 0f) {
                    Audio.Play("event:/game/general/crystalheart_bounce", Position);
                    bounceSfxDelay = 0.1f;
                }
            }
        }

        public void onHoldable(Holdable holdable) {
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && holdable.Dangerous(holdableCollider)) {
                heartBroken(player, holdable, SceneAs<Level>());
            }
        }

        protected abstract void heartBroken(Player player, Holdable holdable, Level level);
    }
}
