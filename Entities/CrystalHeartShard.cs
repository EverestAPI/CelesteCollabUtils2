using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/CrystalHeartShard")]
    class CrystalHeartShard : Entity {
        private Sprite sprite;
        private Sprite white;
        private string spriteName;

        private Wiggler scaleWiggler;

        private Vector2 moveWiggleDir;
        private Wiggler moveWiggler;

        private float bounceSfxDelay;

        public CrystalHeartShard(EntityData data, Vector2 position, EntityID gid)
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
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            AreaKey area = (scene as Level).Session.Area;

            string spritePath = "CollabUtils2/miniheart/" + spriteName + "/";
            if (SaveData.Instance.Areas_Safe[area.ID].Modes[(int) area.Mode].HeartGem) {
                spritePath = "CollabUtils2/miniheart/ghost/ghost";
            }

            Add(sprite = new Sprite(GFX.Game, spritePath));
            sprite.AddLoop("idle", "", 0.08f);
            sprite.Play("idle");
            sprite.CenterOrigin();
        }

        private void OnPlayer(Player player) {
            Level level = Scene as Level;
            if (player.DashAttacking) {
                // player broke the shard
                Add(new Coroutine(SmashRoutine(player, level)));
            } else {
                // player bounces on the shard
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

        private IEnumerator SmashRoutine(Player player, Level level) {
            level.CanRetry = false;

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

            // play the summit gem collect sound
            SoundEmitter.Play("event:/game/07_summit/gem_get", this);

            // overlap a white sprite
            Add(white = new Sprite(GFX.Game, "CollabUtils2/miniheart/white/white"));
            sprite.AddLoop("idle", "", 0.08f);
            sprite.Play("idle");
            sprite.CenterOrigin();

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
            level.RegisterAreaComplete();

            // wait 1 second max
            float timer = 0f;
            while (!Input.MenuConfirm.Pressed && !Input.MenuCancel.Pressed && timer <= 1f) {
                yield return null;
                timer += Engine.DeltaTime;
            }

            // get out of here, back to the lobby
            level.DoScreenWipe(false, () => Engine.Scene = new LevelExitToLobby());
        }

        public override void Update() {
            base.Update();
            bounceSfxDelay -= Engine.DeltaTime;
            sprite.Position = moveWiggleDir * moveWiggler.Value * -8f;

            if (white != null) {
                white.Position = sprite.Position;
                white.Scale = sprite.Scale;
                // white.SetAnimationFrame(sprite.CurrentAnimationFrame);
            }
        }
    }
}
