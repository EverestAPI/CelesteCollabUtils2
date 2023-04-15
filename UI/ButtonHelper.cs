using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.UI {
    public static class ButtonHelper {
        // cached for performance
        private static readonly List<MTexture> multiButtonTextures = new List<MTexture>();

        public static void RenderMultiButton(ref Vector2 position, float xAdvance, ButtonRenderInfo renderInfo, float scale = 1f, float alpha = 1f, float justifyX = 0.5f, float wiggle = 1f, Wiggler wiggler = null) {
            var width = RenderMultiButton(position, renderInfo, scale, alpha, justifyX, wiggle, wiggler);
            if (justifyX < 0.5f)
                position.X += width + xAdvance;
            else
                position.X -= width + xAdvance;
        }

        /// <summary>
        /// Draws a multi button.
        /// </summary>
        public static float RenderMultiButton(Vector2 position, ButtonRenderInfo renderInfo, float scale = 1f, float alpha = 1f, float justifyX = 0.5f, float wiggle = 1f, Wiggler wiggler = null) {
            var textures = getTextures(renderInfo);

            float buttonWidths = 0;
            foreach (var texture in textures) {
                buttonWidths += texture?.Width ?? 0;
            }

            float labelWidth = ActiveFont.Measure(renderInfo.Label).X;
            float fullWidth = labelWidth + 8f + buttonWidths;
            float labelJustifyX = fullWidth / 2f / labelWidth;

            position.X += scale * fullWidth * (0.5f - justifyX);
            wiggle *= (wiggler ?? renderInfo.Wiggler)?.Value ?? 1f;

            drawText(renderInfo.Label, position, labelJustifyX, scale + wiggle, alpha);

            float buttonX = labelWidth + 8f - fullWidth / 2f;
            for (int i = 0; i < textures.Count; i++) {
                if (multiButtonTextures[i] is MTexture texture) {
                    var origin = new Vector2(-buttonX, texture.Height / 2f);
                    buttonX += texture.Width;
                    float ba = renderInfo.AlphaForButtonIndex(i);
                    if (ba > 0) {
                        texture.Draw(position, origin, Color.White * alpha * ba, scale + wiggle);
                    }
                }
            }

            return fullWidth * scale;
        }

        /// <summary>
        /// Draws text for a double button in the specified position.
        /// </summary>
        private static void drawText(string text, Vector2 position, float justifyX, float scale, float alpha) {
            ActiveFont.DrawOutline(text, position, new Vector2(justifyX, 0.5f), Vector2.One * scale, Color.White * alpha, 2f, Color.Black * alpha);
        }

        private static List<MTexture> getTextures(ButtonRenderInfo renderInfo) {
            multiButtonTextures.Clear();
            var fallback = renderInfo.ShowFallback ? "controls/keyboard/oemquestion" : null;
            if (renderInfo.Button1 != null) multiButtonTextures.Add(Input.GuiButton(renderInfo.Button1, fallback));
            if (renderInfo.Button2 != null) multiButtonTextures.Add(Input.GuiButton(renderInfo.Button2, fallback));
            if (renderInfo.Button3 != null) multiButtonTextures.Add(Input.GuiButton(renderInfo.Button3, fallback));
            if (renderInfo.Button4 != null) multiButtonTextures.Add(Input.GuiButton(renderInfo.Button4, fallback));
            return multiButtonTextures;
        }

        public struct ButtonRenderInfo {
            public readonly string Label;
            public readonly VirtualButton Button1;
            public readonly VirtualButton Button2;
            public readonly VirtualButton Button3;
            public readonly VirtualButton Button4;
            public readonly int ButtonCount;
            public readonly bool ShowFallback;
            public readonly Wiggler Wiggler;

            public float Button1Alpha;
            public float Button2Alpha;
            public float Button3Alpha;
            public float Button4Alpha;

            public ButtonRenderInfo(
                string label,
                VirtualButton button1 = null,
                VirtualButton button2 = null,
                VirtualButton button3 = null,
                VirtualButton button4 = null,
                Wiggler wiggler = null,
                bool showFallback = true) {
                Label = label;
                Button1 = button1;
                Button2 = button2;
                Button3 = button3;
                Button4 = button4;
                Wiggler = wiggler;
                ButtonCount = button1 == null ? 0 : button2 == null ? 1 : button3 == null ? 2 : button4 == null ? 3 : 4;
                ShowFallback = showFallback;
                Button1Alpha = Button2Alpha = Button3Alpha = Button4Alpha = 1f;
            }

            public float AlphaForButtonIndex(int index) {
                switch (index) {
                    case 0: return Button1Alpha;
                    case 1: return Button2Alpha;
                    case 2: return Button3Alpha;
                    case 3: return Button4Alpha;
                    default: return 0f;
                }
            }
        }
    }
}
