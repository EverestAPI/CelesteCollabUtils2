using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.Entities {
    class RainbowBerryPerfectEffect : Entity {

        public RainbowBerryPerfectEffect(Vector2 position)
            : base(position) {

            Sprite sprite;
            Depth = -1000000;
            Add(sprite = RainbowBerry.SpriteBank.Create("perfectAnimation"));
            sprite.OnLastFrame = delegate {
                RemoveSelf();
            };
            sprite.Play("perfect");
        }
    }
}
