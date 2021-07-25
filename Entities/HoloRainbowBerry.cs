using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.Entities {

    public class HoloRainbowBerry : Entity {
        private float wobble = 0f;
        private Sprite sprite;
        private Sprite desaturatedSprite;

        private List<ParticleType> particleColors = null;
        private int particleColor = 0;
        private float particleDelay;

        public ParticleSystem Particles { get; private set; }

        private CustomMemorialText counterText;

        public HoloRainbowBerry(Vector2 position, int currentBerries, int totalBerries) {
            float progress = (float) currentBerries / totalBerries;

            // the hologram gets more opaque and saturated as the player gets more silver berries.
            float transparencyProgress = Math.Min(0.9f, progress);
            float saturationProgress = progress * 0.5f;

            // particles are all colors, but match the transparency/saturation of the berry.
            particleColors = new List<ParticleType>();
            for (int i = 0; i < 360; i += 60) {
                particleColors.Add(getParticle(i / 360f, transparencyProgress, saturationProgress));
            }

            // create the sprites, overlap them, and give them transparency.
            sprite = GFX.SpriteBank.Create("CollabUtils2_holoRainbowBerry");
            desaturatedSprite = GFX.SpriteBank.Create("CollabUtils2_desaturatedHoloRainbowBerry");

            sprite.Color *= (transparencyProgress * saturationProgress);
            desaturatedSprite.Color *= transparencyProgress * (1 - saturationProgress);

            Add(sprite);
            Add(desaturatedSprite);

            particleDelay = MathHelper.Lerp(1f, 0.08f, progress);

            Position = position;

            if (currentBerries != 0) {
                // spawn text to show silver berry progress. this is just custom memorial text, the memorial itself being only used to compute the text position.
                string text = $"{currentBerries}/{totalBerries}";
                counterText = new CustomMemorialText(new CustomMemorial(Position + new Vector2(1.5f, 82f), null, "", 16f), false, text, 16f);
            }
        }

        private static ParticleType getParticle(float hue, float transparency, float saturation) {
            Color colorDesaturated = Calc.HsvToColor(hue, saturation, 1);
            Color colorDoubleDesaturated = Calc.HsvToColor(hue, saturation * 0.5f, 1);
            return new ParticleType(Strawberry.P_Glow) {
                Color = colorDesaturated * transparency,
                Color2 = colorDoubleDesaturated * transparency
            };
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            if (counterText != null) {
                scene.Add(counterText);
            }

            scene.Add(Particles = new ParticleSystem(-50000, 800));
            Particles.Tag = Tag;
        }

        public override void Update() {
            base.Update();

            // make the sprite wobble up and down like a berry.
            wobble += Engine.DeltaTime * 4f;
            sprite.Y = (float) Math.Sin(wobble) * 2f;
            desaturatedSprite.Y = sprite.Y;

            // emit particles.
            if (Scene.OnInterval(particleDelay)) {
                ParticleType type = particleColors[particleColor % particleColors.Count];
                Particles.Emit(type, Position + Calc.Random.Range(-Vector2.One * 6f, Vector2.One * 6f));
                particleColor++;
            }

            // show the counter if the player is close (< 50 px), hide it if they aren't.
            if (counterText != null) {
                Player player = Scene.Tracker.GetEntity<Player>();
                counterText.Show = player != null && (player.Position - Position).LengthSquared() < 2500f;
            }
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            if (Particles != null) {
                scene.Remove(Particles);
            }
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
            if (Particles != null) {
                scene.Remove(Particles);
            }
        }
    }
}
