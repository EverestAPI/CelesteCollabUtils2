using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CollabUtils2.Entities {

    /// <summary>
    /// A berry that requires you to go fast to collect it.
    /// </summary>
    [CustomEntity("CollabUtils2/SpeedBerry")]
    [RegisterStrawberry(false, true)]
    [Tracked(true)]
    public class SpeedBerry : Strawberry {

        public static SpriteBank SpriteBank;

        public float BronzeTime;
        public float SilverTime;
        public float GoldTime;

        public float CurrentTime;

        private Vector2 start;
        private Vector2 nearestSpawn;

        /// <summary>
        /// actually just pauses the timer completely
        /// </summary>
        public bool PauseUntilTransition;

        public readonly EntityData EntityData;

        public static void LoadContent() {
            SpriteBank = new SpriteBank(GFX.Game, "Graphics/CollabUtils2/SpeedBerry.xml");
        }

        public SpeedBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) {
            new DynData<Strawberry>(this)["Golden"] = true;
            BronzeTime = data.Float("bronzeTime", 15f);
            SilverTime = data.Float("silverTime", 10f);
            GoldTime = data.Float("goldTime", 5f);
            PauseUntilTransition = data.Bool("pauseUntilTransition", true);
            Follower.PersistentFollow = true;
            EntityData = data;
            startingRoom = data.Level.Name;
            var listener = new TransitionListener() {
                OnOutBegin = () => { PauseUntilTransition = false; SceneAs<Level>().Session.DoNotLoad.Add(ID); }
            };
            Add(listener);
        }

        private string startingRoom;

        public static Dictionary<string, Color> RankColors = new Dictionary<string, Color>()
        {
            { "Bronze", Calc.HexToColor("cd7f32") },
            { "Silver", Color.Silver },
            { "Gold", Color.Gold },
            { "None", Color.Transparent }
        };

        public override void Added(Scene scene) {
            base.Added(scene);
            start = Position;
            // The Speed Berry's gimmick needs to save information about the spawn point nearest its home location.
            nearestSpawn = SceneAs<Level>().GetSpawnPoint(start);
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
        
        public string GetNextRank(out float nextRankTime) {
            float currentTime = CurrentTime;
            string nextRankName;
            if (currentTime < GoldTime) {
                nextRankTime = GoldTime;
                nextRankName = "Gold";
            } else if (currentTime < SilverTime) {
                nextRankTime = SilverTime;
                nextRankName = "Silver";
            } else if (currentTime < BronzeTime) {
                nextRankTime = BronzeTime;
                nextRankName = "Bronze";
            } else {
                // time ran out
                nextRankTime = 0;
                nextRankName = "None";
            }
            return nextRankName;
        }

        public override void Update() {
            Sprite sprite = Get<Sprite>();
            SpeedBerryTimerDisplay timer = Scene.Tracker.GetEntity<SpeedBerryTimerDisplay>();
            if (Follower.HasLeader) {
                sprite.Color = RankColors[GetNextRank(out float n)];
                SpeedBerryTimerDisplay.Enabled = true;

                Player player = Follower.Leader.Entity as Player;
                if (!PauseUntilTransition)
                    CurrentTime += Engine.DeltaTime;
                bool a = player.StrawberriesBlocked;
                if (timer == null) {
                    timer = new SpeedBerryTimerDisplay(this);
                    SceneAs<Level>().Add(timer);
                }
                timer.StopFading();
            } else {
                timer?.StartFading();
                //base.Update();
            }
            if (BronzeTime < CurrentTime) {
                // Time ran out
                TimeRanOut = true;
            }
            if (TimeRanOut) {
                Dissolve();
            }
            // If this Strawberry didn't block normal collection, we would check here to find out if we could collect it.
            // However, since it CAN'T be collected normally, instead we'll check if the Player is overlapping a SpeedBerryCollectTrigger.
            //if (Follower.Leader != null) {
            //    Player player = Follower.Leader.Entity as Player;
            //    if (player.CollideCheck<SpeedBerryCollectTrigger>()) {
            //        OnCollect();
            //    }
            //}
            base.Update();
        }

        public void Dissolve() {
            if (Follower.Leader != null) {
                Player player = Follower.Leader.Entity as Player;
                player.StrawberryCollectResetTimer = 2.5f;
                Add(new Coroutine(DissolveRoutine(player), true));
            } else {
                Add(new Coroutine(DissolveRoutine(null), true));
            }

        }

        private IEnumerator DissolveRoutine(Player follower) {
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

        public bool TimeRanOut;
    }
}
