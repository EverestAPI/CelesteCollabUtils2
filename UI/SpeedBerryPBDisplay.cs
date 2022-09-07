using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.UI {
    public class SpeedBerryPBDisplay : Component {

        public Vector2 Position;
        public MTexture Icon;
        public Color Color;
        public string Text;

        private Vector2 renderPosition => (((Entity != null) ? Entity.Position : Vector2.Zero) + Position).Round();

        public SpeedBerryPBDisplay() : base(true, false) { }

        public override void Render() {
            float textWidth = ActiveFont.Measure(Text).X + 81f;
            Icon.DrawJustified(renderPosition - new Vector2(textWidth / 2f + 15f, 0f), new Vector2(0f, 0.5f));
            ActiveFont.DrawOutline(Text, renderPosition + new Vector2(81f - textWidth / 2f, 0f), new Vector2(0f, 0.5f), Vector2.One, Color, 2f, Color.Black);
        }
    }
}