using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/MiniHeart")]
    public class MiniHeart : AbstractMiniHeart {
        private Sprite white;
        private bool hasBeenBroken = false;
        private readonly bool flash;

        private Coroutine smashRoutine;
        private EventInstance pauseMusicSnapshot;
        private SoundEmitter collectSound;

        public MiniHeart(EntityData data, Vector2 position, EntityID gid)
            : base(data, position, gid) {

            this.flash = data.Bool("flash", defaultValue: true);
        }

        protected override void heartBroken(Player player, Holdable holdable, Level level) {
            if (hasBeenBroken) return;
            
            hasBeenBroken = true;
            Add(smashRoutine = new Coroutine(SmashRoutine(player, level)));
        }

        private IEnumerator SmashRoutine(Player player, Level level) {
            level.CanRetry = false;

            Collidable = false;

            stopMusic();

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
            collectSound = SoundEmitter.Play("event:/SC2020_heartShard_get", this);

            // overlap a white sprite
            Add(white = new Sprite(GFX.Game, "CollabUtils2/miniheart/white/white"));
            white.AddLoop("idle", "", 0.1f, animationFrames);
            white.Play("idle");
            white.CenterOrigin();

            // slow down time, visual effects
            Depth = Depths.FormationSequences;
            yield return null;
            Celeste.Freeze(0.2f);
            yield return null;
            setTimeRate(0.5f);
            player.Depth = Depths.FormationSequences;
            for (int i = 0; i < 10; i++) {
                Scene.Add(new AbsorbOrb(Position));
            }
            level.Shake();
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            if (flash) level.Flash(Color.White);
            light.Alpha = (bloom.Alpha = 0f);
            level.FormationBackdrop.Display = true;
            level.FormationBackdrop.Alpha = 1f;

            // slow down time further, to a freeze
            Visible = false;
            for (float time = 0f; time < 2f; time += Engine.RawDeltaTime) {
                setTimeRate(Calc.Approach(getTimeRate(), 0f, Engine.RawDeltaTime * 0.25f));
                yield return null;
            }

            // make sure update order with the player is just right
            Depth = 0;
            Depth = Depths.FormationSequences;

            yield return null;
            if (player.Dead) {
                yield return 100f;
            }

            setTimeRate(1f);
            Tag = Tags.FrozenUpdate;
            level.Frozen = true;

            // level is done! stop timer, save completion
            SaveData.Instance.RegisterHeartGem(level.Session.Area);
            level.TimerStopped = true;
            level.PauseLock = true;
            level.RegisterAreaComplete();

            // music is definitively muted at this point. It shouldn't come back when the muted snapshot is released.
            Audio.SetMusic(null);
            Audio.SetAmbience(null);

            // display the end screen and get out of here!
            yield return new SwapImmediately(ReturnToLobbyHelper.DisplayCollabMapEndScreenIfEnabled());
            ReturnToLobbyHelper.TriggerReturnToLobby();
        }

#pragma warning disable CS0618 // Switching to a TimeRateModifier desyncs TASes
        private static void setTimeRate(float timeRate) => Engine.TimeRate = timeRate;
        private static float getTimeRate() => Engine.TimeRate;
#pragma warning restore CS0618

        public override void Update() {
            base.Update();

            if (white != null) {
                white.Position = sprite.Position;
                white.Scale = sprite.Scale;
                white.SetAnimationFrame(sprite.CurrentAnimationFrame);
            }

            if (hasBeenBroken && (Scene.Tracker.GetEntity<Player>()?.Dead ?? true)) {
                interruptCollection();
            }
        }

        private void interruptCollection() {
            Level level = Scene as Level;
            level.Frozen = false;
            level.CanRetry = true;
            level.FormationBackdrop.Display = false;
            setTimeRate(1f);

            if (collectSound != null) {
                collectSound.RemoveSelf();
                collectSound = null;
            }

            if (smashRoutine != null) {
                smashRoutine.RemoveSelf();
                smashRoutine = null;
            }
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);

            // resume music when the player respawns.
            resumeMusic();
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);

            // we don't want the music to be muted forever!
            resumeMusic();
        }

        private void stopMusic() {
            if (pauseMusicSnapshot == null) {
                pauseMusicSnapshot = Audio.CreateSnapshot("snapshot:/music_mains_mute");
            }
            Audio.BusStopAll("bus:/gameplay_sfx", immediate: true);
        }

        private void resumeMusic() {
            if (pauseMusicSnapshot != null) {
                Audio.ReleaseSnapshot(pauseMusicSnapshot);
                pauseMusicSnapshot = null;
            }
        }
    }
}
