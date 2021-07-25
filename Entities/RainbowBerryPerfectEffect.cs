using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.Entities {
    public class RainbowBerryPerfectEffect : Entity {

        public RainbowBerryPerfectEffect(Vector2 position)
            : base(position) {

            Sprite sprite;
            Depth = -1000000;
            Add(sprite = GFX.SpriteBank.Create("CollabUtils2_perfectAnimation"));
            sprite.OnLastFrame = delegate {
                RemoveSelf();
            };
            sprite.Play("perfect");
        }
    }
}
