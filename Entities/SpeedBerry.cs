using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.Entities {

    /// <summary>
    /// A berry that requires you to go fast to collect it.
    /// </summary>
    [CustomEntity("CollabUtils2/SpeedBerry")]
    [Tracked]
    public class SpeedBerry : Strawberry {

        public static SpriteBank SpriteBank;

        public EntityData EntityData;
        public float BronzeTime;
        public float SilverTime;
        public float GoldTime;
        public bool TimeRanOut;

        public SpeedBerryTimerDisplay TimerDisplay;

        private bool transitioned = false;

        // TODO delete once definitive speedberry sprites are in place. they probably won't use tinting
        private static Dictionary<string, Color> RankColors = new Dictionary<string, Color>() {
            { "Bronze", Calc.HexToColor("cd7f32") },
            { "Silver", Color.Silver },
            { "Gold", Color.Gold },
            { "None", Color.Transparent }
        };

        public static void LoadContent() {
            SpriteBank = new SpriteBank(GFX.Game, "Graphics/CollabUtils2/SpeedBerry.xml");
        }

        public SpeedBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) {
            EntityData = data;
            new DynData<Strawberry>(this)["Golden"] = true;
            BronzeTime = data.Float("bronzeTime", 15f);
            SilverTime = data.Float("silverTime", 10f);
            GoldTime = data.Float("goldTime", 5f);
            Follower.PersistentFollow = true;
            var listener = new TransitionListener() {
                OnOutBegin = () => {
                    SceneAs<Level>().Session.DoNotLoad.Add(ID);
                    transitioned = true;
                }
            };
            Add(listener);
        }

        public override void Awake(Scene scene) {
            Session session = (scene as Level).Session;
            if (!(SaveData.Instance.CheatMode || SaveData.Instance.Areas_Safe[session.Area.ID].Modes[(int) session.Area.Mode].Completed)) {
                // the berry shouldn't spawn
                RemoveSelf();
                return;
            }
            base.Awake(scene);
        }

        public override void Update() {
            Sprite sprite = Get<Sprite>();

            if (Follower.HasLeader) {
                if (TimerDisplay == null) {
                    TimerDisplay = new SpeedBerryTimerDisplay(this);
                    SceneAs<Level>().Add(TimerDisplay);
                }

                if ((Follower.Leader.Entity as Player)?.CollideCheck<SpeedBerryCollectTrigger>() ?? false) {
                    // collect the speed berry!
                    TimerDisplay.EndTimer();
                    OnCollect();
                }
            }

            if (TimerDisplay != null) {
                sprite.Color = RankColors[TimerDisplay.GetNextRank(out _)];

                if (BronzeTime < TimeSpan.FromTicks(TimerDisplay.GetSpentTime()).TotalSeconds) {
                    // Time ran out
                    TimeRanOut = true;
                }
            }

            if (TimeRanOut) {
                dissolve();
            }

            if (transitioned) {
                transitioned = false;
                TimerDisplay?.StartTimer();
            }

            base.Update();
        }

        private void dissolve() {
            if (Follower.Leader != null) {
                Player player = Follower.Leader.Entity as Player;
                player.StrawberryCollectResetTimer = 2.5f;
                Add(new Coroutine(dissolveRoutine(player), true));
            } else {
                Add(new Coroutine(dissolveRoutine(null), true));
            }

        }

        private IEnumerator dissolveRoutine(Player follower) {
            Sprite sprite = Get<Sprite>();
            Level level = Scene as Level;
            Session session = level.Session;
            session.DoNotLoad.Remove(ID);
            Collidable = false;
            sprite.Scale = Vector2.One * 0.5f;
            if (follower != null) {
                foreach (Player player in Scene.Tracker.GetEntities<Player>()) {
                    player.Die(Vector2.Zero, true, true);
                }
            }
            yield return 0.05f;
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            for (int i = 0; i < 6; i++) {
                float dir = Calc.Random.NextFloat(6.28318548f);
                level.ParticlesFG.Emit(StrawberrySeed.P_Burst, 1, Position + Calc.AngleToVector(dir, 4f), Vector2.Zero, dir);
            }
            sprite.Scale = Vector2.Zero;
            Visible = false;
            RemoveSelf();
            yield break;
        }
    }
}
