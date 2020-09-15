using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/MiniHeart")]
    class MiniHeart : Entity {
        private static readonly int[] animationFrames = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

        private Sprite sprite;
        private Sprite white;
        private string spriteName;
        private bool inCollectAnimation = false;

        private Wiggler scaleWiggler;

        private Vector2 moveWiggleDir;
        private Wiggler moveWiggler;

        private float bounceSfxDelay;

        private VertexLight light;
        private BloomPoint bloom;
        private ParticleType shineParticle;

        private HoldableCollider holdableCollider;

        public MiniHeart(EntityData data, Vector2 position, EntityID gid)
            : base(data.Position + position) {

            spriteName = data.Attr("sprite");

            Collider = new Hitbox(12f, 12f, -6f, -6f);

            Add(scaleWiggler = Wiggler.Create(0.5f, 4f, f => {
                sprite.Scale = Vector2.One * (1f + f * 0.3f);
            }));
            moveWiggler = Wiggler.Create(0.8f, 2f);
            moveWiggler.StartZero = true;
            Add(moveWiggler);

            Add(new PlayerCollider(OnPlayer));
            Add(holdableCollider = new HoldableCollider(OnHoldable));
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            AreaKey area = (scene as Level).Session.Area;

            string spritePath = "CollabUtils2/miniheart/" + spriteName + "/";
            bool alreadyCollectedInSave = SaveData.Instance.Areas_Safe[area.ID].Modes[(int) area.Mode].HeartGem;
            if (alreadyCollectedInSave) {
                spritePath = "CollabUtils2/miniheart/ghost/ghost";
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

        private void OnPlayer(Player player) {
            Level level = Scene as Level;
            if (player.DashAttacking) {
                // player broke the heart
                Add(new Coroutine(SmashRoutine(player, level)));
            } else {
                // player bounces on the heart
                player.PointBounce(Center);
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

        public void OnHoldable(Holdable holdable) {
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null && holdable.Dangerous(holdableCollider)) {
                Add(new Coroutine(SmashRoutine(player, SceneAs<Level>())));
            }
        }

        private IEnumerator SmashRoutine(Player player, Level level) {
            level.CanRetry = false;
            inCollectAnimation = true;

            Collidable = false;

            // mute sound
            Audio.SetMusic(null);
            Audio.SetAmbience(null);

            // collect all berries
            List<IStrawberry> list = new List<IStrawberry>();
            ReadOnlyCollection<Type> berryTypes = StrawberryRegistry.GetBerryTypes();
            foreach (Follower follower in player.Leader.Followers) {
                if (berryTypes.Contains(follower.Entity.GetType()) && follower.Entity is IStrawberry) {
                    list.Add(follower.Entity as IStrawberry);
                }
            }
            foreach (IStrawberry item in list) {
                item.OnCollect();
            }

            // play the collect jingle
            SoundEmitter.Play("event:/SC2020_heartShard_get", this);

            // overlap a white sprite
            Add(white = new Sprite(GFX.Game, "CollabUtils2/miniheart/white/white"));
            white.AddLoop("idle", "", 0.1f, animationFrames);
            white.Play("idle");
            white.CenterOrigin();

            // slow down time, visual effects
            Depth = -2000000;
            yield return null;
            Celeste.Freeze(0.2f);
            yield return null;
            Engine.TimeRate = 0.5f;
            player.Depth = -2000000;
            for (int i = 0; i < 10; i++) {
                Scene.Add(new AbsorbOrb(Position));
            }
            level.Shake();
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            level.Flash(Color.White);
            light.Alpha = (bloom.Alpha = 0f);
            level.FormationBackdrop.Display = true;
            level.FormationBackdrop.Alpha = 1f;

            // slow down time further, to a freeze
            Visible = false;
            for (float time = 0f; time < 2f; time += Engine.RawDeltaTime) {
                Engine.TimeRate = Calc.Approach(Engine.TimeRate, 0f, Engine.RawDeltaTime * 0.25f);
                yield return null;
            }
            yield return null;
            if (player.Dead) {
                yield return 100f;
            }

            Engine.TimeRate = 1f;
            Tag = Tags.FrozenUpdate;
            level.Frozen = true;

            // level is done! stop timer, save completion
            SaveData.Instance.RegisterHeartGem(level.Session.Area);
            level.TimerStopped = true;
            level.PauseLock = true;
            level.RegisterAreaComplete();

            // display an endscreen if enabled in mod options AND speedrun timer is enabled (or else the endscreen won't show anything anyway).
            if (CollabModule.Instance.Settings.DisplayEndScreenForAllMaps && Settings.Instance.SpeedrunClock != SpeedrunType.Off) {
                Scene.Add(new AreaCompleteInfoInLevel());

                // force the player to wait a bit, so that the info shows up
                yield return 0.5f;

                // wait for an input
                while (!Input.MenuConfirm.Pressed && !Input.MenuCancel.Pressed) {
                    yield return null;
                }
            } else {
                // wait 1 second max
                float timer = 0f;
                while (!Input.MenuConfirm.Pressed && !Input.MenuCancel.Pressed && timer <= 1f) {
                    yield return null;
                    timer += Engine.DeltaTime;
                }
            }

            // get out of here, back to the lobby
            level.DoScreenWipe(false, () => Engine.Scene = new LevelExitToLobby(LevelExit.Mode.Completed, level.Session));
        }

        public override void Update() {
            base.Update();
            bounceSfxDelay -= Engine.DeltaTime;
            sprite.Position = moveWiggleDir * moveWiggler.Value * -8f;

            if (white != null) {
                white.Position = sprite.Position;
                white.Scale = sprite.Scale;
                white.SetAnimationFrame(sprite.CurrentAnimationFrame);
            }

            if (Visible && Scene.OnInterval(0.1f)) {
                SceneAs<Level>().Particles.Emit(shineParticle, 1, Center + sprite.Position, Vector2.One * 4f);
            }

            if (inCollectAnimation && (Scene.Tracker.GetEntity<Player>()?.Dead ?? true)) {
                InterruptCollection();
            }
        }

        private void InterruptCollection() {
            Level level = Scene as Level;
            level.Frozen = false;
            level.CanRetry = true;
            level.FormationBackdrop.Display = false;
            Engine.TimeRate = 1f;
            RemoveSelf();
        }
    }
}
