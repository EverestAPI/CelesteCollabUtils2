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
        private bool inCollectAnimation = false;

        private Coroutine smashRoutine;
        private EventInstance pauseMusicSnapshot;
        private SoundEmitter collectSound;

        public MiniHeart(EntityData data, Vector2 position, EntityID gid)
            : base(data, position, gid) { }

        protected override void heartBroken(Player player, Holdable holdable, Level level) {
            Add(smashRoutine = new Coroutine(SmashRoutine(player, level)));
        }

        private IEnumerator SmashRoutine(Player player, Level level) {
            level.CanRetry = false;
            inCollectAnimation = true;

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
            Engine.TimeRate = 0.5f;
            player.Depth = Depths.FormationSequences;
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

            // music is definitively muted at this point. It shouldn't come back when the muted snapshot is released.
            Audio.SetMusic(null);
            Audio.SetAmbience(null);

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

            if (white != null) {
                white.Position = sprite.Position;
                white.Scale = sprite.Scale;
                white.SetAnimationFrame(sprite.CurrentAnimationFrame);
            }

            if (inCollectAnimation && (Scene.Tracker.GetEntity<Player>()?.Dead ?? true)) {
                interruptCollection();
            }
        }

        private void interruptCollection() {
            Level level = Scene as Level;
            level.Frozen = false;
            level.CanRetry = true;
            level.FormationBackdrop.Display = false;
            Engine.TimeRate = 1f;

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
            pauseMusicSnapshot = Audio.CreateSnapshot("snapshot:/music_mains_mute");
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
