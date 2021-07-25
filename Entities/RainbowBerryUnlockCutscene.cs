using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.Entities {
    class RainbowBerryUnlockCutscene : CutsceneEntity {
        private RainbowBerry strawberry;
        private HoloRainbowBerry holoBerry;
        private int silverBerryCount;
        private ParticleSystem system;
        private EventInstance snapshot;
        private EventInstance sfx;

        private Image[] silverBerries;

        public RainbowBerryUnlockCutscene(RainbowBerry strawberry, HoloRainbowBerry holoBerry, int silverBerryCount) {
            this.strawberry = strawberry;
            this.holoBerry = holoBerry;
            this.silverBerryCount = silverBerryCount;
        }

        public override void OnBegin(Level level) {
            Add(new Coroutine(Cutscene(level)));
        }

        private IEnumerator Cutscene(Level level) {
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null) {
                // wait until player is in control (respawn animation done, etc).
                while (!player.InControl) {
                    yield return null;
                }
                player.StateMachine.State = Player.StDummy;
            }

            // start animation, mute sound
            sfx = Audio.Play("event:/game/general/seed_complete_main", Position);
            snapshot = Audio.CreateSnapshot("snapshot:/music_mains_mute");

            // spawn silver berries, not visible yet
            silverBerries = new Image[silverBerryCount];
            for (int i = 0; i < silverBerryCount; i++) {
                silverBerries[i] = new Image(GFX.Game["CollabUtils2/silverBerry/idle00"]);
                silverBerries[i].Color = Color.White * 0;
                silverBerries[i].CenterOrigin();
                if (player != null) {
                    silverBerries[i].Position = player.Position;
                }
                Add(silverBerries[i]);
            }

            // freeze the level after a bit
            Depth = Depths.FormationSequences - 3;
            strawberry.Depth = Depths.FormationSequences - 2;
            holoBerry.Depth = -Depths.FormationSequences - 2;
            holoBerry.Particles.Depth = Depths.FormationSequences - 2;
            strawberry.AddTag(Tags.FrozenUpdate);
            yield return 0.35f;
            Tag = Tags.FrozenUpdate;
            level.Frozen = true;

            // darken the level, pause SFX
            level.FormationBackdrop.Display = true;
            level.FormationBackdrop.Alpha = 0.5f;
            level.Displacement.Clear();
            level.Displacement.Enabled = false;
            Audio.BusPaused(Buses.AMBIENCE, true);
            Audio.BusPaused(Buses.CHAR, true);
            Audio.BusPaused(Buses.YES_PAUSE, true);
            Audio.BusPaused(Buses.CHAPTERS, true);
            yield return 0.1f;

            system = new ParticleSystem(Depths.FormationSequences - 2, 50);
            system.Tag = Tags.FrozenUpdate;
            level.Add(system);

            // start the spin
            float angleSep = (float) Math.PI * 2f / silverBerryCount;
            float angle = (float) Math.PI / 2f;
            foreach (Image silverBerry in silverBerries) {
                startSpinAnimation(silverBerry, silverBerry.Position, strawberry.Position, angle, 4f);
                angle -= angleSep;
            }

            // focus camera on rainbow berry and wait
            Vector2 cameraTarget = strawberry.Position - new Vector2(160f, 90f);
            cameraTarget = cameraTarget.Clamp(level.Bounds.Left, level.Bounds.Top, level.Bounds.Right - 320, level.Bounds.Bottom - 180);
            yield return 0.1f;
            Add(new Coroutine(CameraTo(cameraTarget, 2f, Ease.CubeInOut)));
            yield return 3.9f;

            // combine all silvers into the rainbow
            Input.Rumble(RumbleStrength.Light, RumbleLength.Long);
            Audio.Play("event:/game/general/seed_complete_berry", strawberry.Position);
            foreach (Image silverBerry in silverBerries) {
                startCombineAnimation(silverBerry, strawberry.Position, 0.6f, system);
            }
            yield return 0.6f;

            // remove the silver berries, and make the rainbow berry appear
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            foreach (Image silverBerry in silverBerries) {
                silverBerry.RemoveSelf();
            }
            holoBerry.RemoveSelf();
            strawberry.CollectedSeeds();
            yield return 0.5f;

            // pan back to the player
            yield return CameraTo(player.CameraTarget, 1f, Ease.CubeOut);

            // cutscene is over!
            level.EndCutscene();
            OnEnd(level);
        }

        public override void OnEnd(Level level) {
            if (WasSkipped) {
                Audio.Stop(sfx);
            }
            Player player = Scene.Tracker.GetEntity<Player>();
            if (player != null) {
                player.StateMachine.State = Player.StNormal;
            }
            level.OnEndOfFrame += delegate {
                if (WasSkipped) {
                    if (silverBerries != null) {
                        foreach (Image silverBerry in silverBerries) {
                            silverBerry.RemoveSelf();
                        }
                    }
                    holoBerry.RemoveSelf();
                    strawberry.CollectedSeeds();
                    level.Camera.Position = player.CameraTarget;
                }
                strawberry.Depth = Depths.Pickups;
                strawberry.RemoveTag(Tags.FrozenUpdate);
                level.Frozen = false;
                level.FormationBackdrop.Display = false;
                level.Displacement.Enabled = true;
            };
            RemoveSelf();
        }

        private void endSfx() {
            Audio.BusPaused(Buses.AMBIENCE, false);
            Audio.BusPaused(Buses.CHAR, false);
            Audio.BusPaused(Buses.YES_PAUSE, false);
            Audio.BusPaused(Buses.CHAPTERS, false);
            Audio.ReleaseSnapshot(snapshot);
        }

        private void startSpinAnimation(Image silverBerry, Vector2 averagePos, Vector2 centerPos, float angleOffset, float time) {
            float spinLerp = 0f;
            Vector2 start = silverBerry.Position;

            // first tween to make the lerp progress
            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, time / 2f, start: true);
            tween.OnUpdate = t => {
                spinLerp = t.Eased;
            };
            Add(tween);

            // second tween to make the berry spin and fade in
            tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, time, start: true);
            tween.OnUpdate = t => {
                float angleRadians = (float) Math.PI / 2f + angleOffset - MathHelper.Lerp(0f, 32.2013245f, t.Eased);
                Vector2 rotationCenter = Vector2.Lerp(averagePos, centerPos, spinLerp);
                Vector2 berryPosition = rotationCenter + Calc.AngleToVector(angleRadians, 25f);
                silverBerry.Position = Vector2.Lerp(start, berryPosition, spinLerp);
                silverBerry.Color = Color.White * spinLerp;
            };
            Add(tween);
        }

        private void startCombineAnimation(Image silverBerry, Vector2 centerPos, float time, ParticleSystem particleSystem) {
            Vector2 position = silverBerry.Position;
            float startAngle = Calc.Angle(centerPos, position);
            Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.BigBackIn, time, start: true);

            // the tween should pull the silver towards the center and rotate it at the same time.
            tween.OnUpdate = t => {
                float angleRadians = MathHelper.Lerp(startAngle, startAngle - (float) Math.PI * 2f, Ease.CubeIn(t.Percent));
                float length = MathHelper.Lerp(25f, 0f, t.Eased);
                silverBerry.Position = centerPos + Calc.AngleToVector(angleRadians, length);
            };

            // when done, emit particles and make the seed disappear.
            tween.OnComplete = delegate {
                silverBerry.Visible = false;
                for (int i = 0; i < 6; i++) {
                    float particleDirection = Calc.Random.NextFloat((float) Math.PI * 2f);
                    particleSystem.Emit(StrawberrySeed.P_Burst, 1, silverBerry.Position + Calc.AngleToVector(particleDirection, 4f), Vector2.Zero, particleDirection);
                }
                silverBerry.RemoveSelf();
            };

            Add(tween);
        }

        public override void Removed(Scene scene) {
            endSfx();
            base.Removed(scene);
        }

        public override void SceneEnd(Scene scene) {
            endSfx();
            base.SceneEnd(scene);
        }
    }
}
