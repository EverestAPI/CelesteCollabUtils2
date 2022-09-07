using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;

namespace Celeste.Mod.CollabUtils2.Entities {

    /// <summary>
    /// A berry that requires you to go fast to collect it.
    /// </summary>
    [CustomEntity("CollabUtils2/SpeedBerry")]
    [Tracked]
    public class SpeedBerry : Strawberry {
        public EntityData EntityData;
        public float BronzeTime;
        public float SilverTime;
        public float GoldTime;
        public bool TimeRanOut;

        private static ParticleType P_OrigGoldGlow;
        private static ParticleType P_SilverGlow;
        private static ParticleType P_BronzeGlow;

        public SpeedBerryTimerDisplay TimerDisplay;

        private Sprite sprite;

        private bool collected = false;
        public bool restored = false;

        private Vector2 transitionStart;
        private Vector2 transitionTarget;
        private bool forcePositionToTransitionTarget;

        private static bool transitionJustTriggered = false;

        internal static void Load() {
            On.Celeste.Level.NextLevel += onTransitionTriggered;
            On.Celeste.Player.OnTransition += onTransitionEnd;
        }

        internal static void Unload() {
            On.Celeste.Level.NextLevel -= onTransitionTriggered;
            On.Celeste.Player.OnTransition -= onTransitionEnd;
        }

        private static void onTransitionTriggered(On.Celeste.Level.orig_NextLevel orig, Level self, Vector2 at, Vector2 dir) {
            orig(self, at, dir);

            // this flag will prevent the speed berry from exploding on the same frame as a transition, because that causes a crash.
            transitionJustTriggered = true;
        }

        private static void onTransitionEnd(On.Celeste.Player.orig_OnTransition orig, Player self) {
            orig(self);

            SpeedBerry speedBerry = self.Scene?.Tracker.GetEntity<SpeedBerry>();
            if (speedBerry != null) {
                speedBerry.TimerDisplay?.StartTimer();
            }
        }

        public SpeedBerry(EntityData data, Vector2 offset, EntityID id, bool restored) : base(data, offset, id) {
            EntityData = data;
            new DynData<Strawberry>(this)["Golden"] = true;
            BronzeTime = data.Float("bronzeTime", 15f);
            SilverTime = data.Float("silverTime", 10f);
            GoldTime = data.Float("goldTime", 5f);
            this.restored = restored;
            Follower.PersistentFollow = true;
            var listener = new TransitionListener() {
                OnOutBegin = () => {
                    SceneAs<Level>()?.Session.DoNotLoad.Add(ID);
                    transitionStart = Position;
                    transitionTarget = new Vector2(
                        Calc.Clamp(transitionStart.X, SceneAs<Level>().Bounds.Left + 8f, SceneAs<Level>().Bounds.Right - 8f),
                        Calc.Clamp(transitionStart.Y, SceneAs<Level>().Bounds.Top + 8f, SceneAs<Level>().Bounds.Bottom - 8f));
                },
                OnOut = percent => {
                    if (SceneAs<Level>()?.Tracker.GetEntity<Player>()?.CollideCheck<SpeedBerryCollectTrigger>() ?? false) {
                        // as soon as the transition is over, the berry will be collected, so be sure to drag it on the new screen
                        // so that the player actually sees it collect.
                        Position = Vector2.Lerp(transitionStart, transitionTarget, Ease.SineOut(percent));
                        forcePositionToTransitionTarget = true;
                    }
                }
            };
            Add(listener);

            if (P_SilverGlow == null) {
                P_SilverGlow = new ParticleType(P_Glow) {
                    Color = Calc.HexToColor("B3AF9F"),
                    Color2 = Calc.HexToColor("E6DCB5")
                };
                P_BronzeGlow = new ParticleType(P_Glow) {
                    Color = Calc.HexToColor("9A5B3C"),
                    Color2 = Calc.HexToColor("ECAF66")
                };
                P_OrigGoldGlow = P_GoldGlow;
            }

            // the speed berry most certainly shouldn't flip the "grabbed golden" switch in session.
            PlayerCollider collider = Get<PlayerCollider>();
            Action<Player> onPlayer = collider.OnCollide;
            collider.OnCollide = player => {
                bool bakGrabbedGolden = (Scene as Level).Session.GrabbedGolden;
                onPlayer(player);
                (Scene as Level).Session.GrabbedGolden = bakGrabbedGolden;
            };
        }

        public SpeedBerry(EntityData data, Vector2 offset, EntityID id) : this(data, offset, id, restored: false) { }

        public override void Awake(Scene scene) {
            Session session = (scene as Level).Session;
            if (!restored && (session.FurthestSeenLevel != session.Level ||
                (!SaveData.Instance.CheatMode && !SaveData.Instance.Areas_Safe[session.Area.ID].Modes[(int) session.Area.Mode].Completed))) {

                // the berry shouldn't spawn
                RemoveSelf();
                return;
            }
            base.Awake(scene);

            sprite = new DynData<Strawberry>(this).Get<Sprite>("sprite");
            if (TimerDisplay != null) {
                string nextRank = TimerDisplay.GetNextRank(out _).ToLowerInvariant();
                if (nextRank != "none") {
                    playAnimationSpeedrunToolSafe(sprite, "idle_" + nextRank);
                }
            }
        }

        public override void Update() {
            Sprite sprite = Get<Sprite>();

            if (Follower.HasLeader) {
                if (TimerDisplay == null) {
                    TimerDisplay = new SpeedBerryTimerDisplay(this);
                    SceneAs<Level>().Add(TimerDisplay);
                }

                // show a message about customizing the speed berry timer position when grabbing it for the first time in the save.
                Player player = Scene.Tracker.GetEntity<Player>();
                if (player != null && !CollabModule.Instance.SaveData.SpeedberryOptionMessageShown) {
                    CollabModule.Instance.SaveData.SpeedberryOptionMessageShown = true;
                    Scene.Add(new DialogCutscene("collabutils2_speedberry_optionmessage", player, false));
                }
            }

            if (TimerDisplay != null && !collected) {
                if (BronzeTime < TimeSpan.FromTicks(TimerDisplay.GetSpentTime()).TotalSeconds) {
                    // Time ran out
                    TimeRanOut = true;
                } else if (Follower.Leader?.Entity is Player leaderPlayer && !leaderPlayer.Dead && leaderPlayer.CollideCheck<SpeedBerryCollectTrigger>()) {
                    // collect the speed berry!
                    TimerDisplay.EndTimer();
                    OnCollect();
                    collected = true;
                } else {
                    string nextRank = TimerDisplay.GetNextRank(out float nextTime).ToLowerInvariant();
                    // the berry is close to exploding, time is over in 1.2 seconds. Start the exploding animation
                    if (nextRank == "bronze" && sprite.CurrentAnimationID != "explosion"
                        && TimeSpan.FromTicks(TimerDisplay.GetSpentTime()).TotalMilliseconds + 1200 > nextTime * 1000) {

                        playAnimationSpeedrunToolSafe(sprite, "explosion");
                    }

                    // the current animation does not match the expected animation.
                    if (nextRank != "gold" && sprite.CurrentAnimationID != "explosion" && !sprite.CurrentAnimationID.Contains(nextRank)) {
                        playAnimationSpeedrunToolSafe(sprite, "transition_to_" + nextRank);
                    }

                    if (nextRank == "bronze") {
                        P_GoldGlow = P_BronzeGlow;
                    } else if (nextRank == "silver") {
                        P_GoldGlow = P_SilverGlow;
                    }
                }
            }

            if (TimeRanOut && !transitionJustTriggered) {
                dissolve();
            }
            transitionJustTriggered = false;

            base.Update();

            P_GoldGlow = P_OrigGoldGlow;

            if (forcePositionToTransitionTarget) {
                Position = transitionTarget;
            }

            // replace the collect animation with the matching color collect animation.
            if (sprite.CurrentAnimationID == "collect") {
                playAnimationSpeedrunToolSafe(sprite, "collect_" + TimerDisplay.GetNextRank(out _).ToLowerInvariant());
            }
        }

        private static void playAnimationSpeedrunToolSafe(Sprite sprite, string animation) {
            if (sprite.Texture?.AtlasPath?.StartsWith("collectables/speedrun_tool_goldberry/") ?? false) {
                // this means Speedrun Tool replaced the sprite, so replace it again or we are going to crash.
                GFX.SpriteBank.CreateOn(sprite, "CollabUtils2_speedBerry");
            }
            sprite.Play(animation);
        }

        private void dissolve() {
            Level level = Scene as Level;
            level.Session.DoNotLoad.Remove(ID);
            Collidable = false;
            foreach (Player player in Scene.Tracker.GetEntities<Player>()) {
                player.Die(Vector2.Zero, true, true);
            }
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            Visible = false;
            RemoveSelf();

            Scene.Add(new SpeedBerryExplosionAnimation(Position));
        }

        private class SpeedBerryExplosionAnimation : Entity {
            public SpeedBerryExplosionAnimation(Vector2 position) : base(position) {
                Add(GFX.SpriteBank.Create("CollabUtils2_speedBerryExplosion"));
                Depth = Depths.Top;
            }
        }
    }
}
