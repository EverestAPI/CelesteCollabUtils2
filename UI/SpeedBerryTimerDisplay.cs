using Celeste.Mod.CollabUtils2.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CollabUtils2.UI {
    [Tracked]
    public class SpeedBerryTimerDisplay : Entity {
        public SpeedBerry TrackedBerry;
        public static SpeedBerryTimerDisplay Instance;
        public static float FadeTime = 2f;
        public static bool Fading { get; private set; }
        public static bool Enabled;
        public SpeedBerryTimerDisplay(SpeedBerry berr) {
            Instance = this;
            CompleteTimer = 0f;
            bg = GFX.Gui["strawberryCountBG"];
            DrawLerp = 0f;
            Tag = (Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate);
            Depth = -100;
            Position = new Vector2(Engine.Width / 2f, -100f);
            Fading = true;
            StopFading();
            CalculateBaseSizes();
            Add(wiggler = Wiggler.Create(0.5f, 4f, null, false, false));
            TrackedBerry = berr;
            FadeTime = 2.5f;
            Fading = false;
        }

        static PixelFontSize pixelFontSize;

        public static void CalculateBaseSizes() {
            if (zeroTimeMeasure != null) {
                PixelFont font = Dialog.Languages["english"].Font;
                float fontFaceSize = Dialog.Languages["english"].FontFaceSize;
                pixelFontSize = font.Get(fontFaceSize);
                for (int i = 0; i < 10; i++) {
                    float x = pixelFontSize.Measure(i.ToString()).X;
                    bool flag = x > numberWidth;
                    if (flag) {
                        numberWidth = x;
                    }
                }
                spacerWidth = pixelFontSize.Measure('.').X;

                zeroTimeMeasure = pixelFontSize.Measure("000") + pixelFontSize.Measure(":.000").X * 0.7f * Vector2.UnitX;
                rankMeasurements = new Dictionary<string, Vector2>() {
                { "Gold", pixelFontSize.Measure("Gold: ")},
                { "Silver", pixelFontSize.Measure("Silver: ")},
                { "Bronze", pixelFontSize.Measure("Bronze: ")},
                { "None", Vector2.Zero } };
            }
        }

        public override void Update() {
            DrawLerp = Calc.Approach(DrawLerp, 1f, Engine.DeltaTime * 4f);
            if (Fading) {
                FadeTime -= Engine.DeltaTime;
                if (FadeTime <= 0f) {
                    Enabled = false;
                    SceneAs<Level>().Remove(this);
                } else if (!tweenStarted && FadeTime <= 1.5f) {
                    tween.Start();
                    tweenStarted = true;
                }

            }

            base.Update();
        }

        public void StartFading() {
            if (!Fading) {
                Fading = true;
                FadeTime = 3f;
                Vector2 start = Position;
                Vector2 end = new Vector2(Position.X, -100f);
                Get<Tween>()?.RemoveSelf();
                tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 1.4f);
                tween.OnUpdate = delegate (Tween t) {
                    Position = Vector2.Lerp(start, end, t.Eased);
                };
                Add(tween);
            }
        }

        public void StopFading() {
            if (Fading) {
                Fading = false;
                FadeTime = 3f;
                Vector2 start = Position;
                Vector2 end = new Vector2(Position.X, 60f);
                Get<Tween>()?.RemoveSelf();
                tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 0.6f, true);
                tween.OnUpdate = delegate (Tween t) {
                    Position = Vector2.Lerp(start, end, t.Eased);
                };
                Add(tween);
            }
        }

        bool tweenStarted;
        Tween tween;
        static Vector2 zeroTimeMeasure;
        static Dictionary<string, Vector2> rankMeasurements;

        public override void Render() {
            if (Enabled && !(DrawLerp <= 0f) && FadeTime > 0f && !TrackedBerry.TimeRanOut) {
                // get next best rank time
                string nextRankName = TrackedBerry.GetNextRank(out float nextRankTime);
                string timeString = TimeSpan.FromSeconds(TrackedBerry.CurrentTime).ShortGameplayFormat(); //FormatTime(TrackedBerry.CurrentTime);
                string timeString2 = $"{nextRankName}: {TimeSpan.FromSeconds(nextRankTime).ShortGameplayFormat()}";
                // Current time
                DrawTime(new Vector2(Position.X - (zeroTimeMeasure.X / 2), Y + 44f), timeString, SpeedBerry.RankColors[nextRankName], 1f + wiggler.Value * 0.15f, 1f);
                // draw next best rank time
                float scale = 1f + wiggler.Value * 0.15f;
                Vector2 measure = zeroTimeMeasure + rankMeasurements[nextRankName].X * Vector2.UnitX;
                DrawTime(new Vector2(Position.X - (measure.X / 2), Y + 44f + (measure.Y * 1.1f)), timeString2, SpeedBerry.RankColors[nextRankName], scale, 1f);
            }
        }

        public static void DrawTime(Vector2 position, string timeString, Color color, float scale = 1f, float alpha = 1f) {
            PixelFont font = Dialog.Languages["english"].Font;
            float fontFaceSize = Dialog.Languages["english"].FontFaceSize;
            float num = scale;
            float num2 = position.X;
            float num3 = position.Y;
            color = color * alpha;
            Color color2 = color * alpha;

            foreach (char c in timeString) {
                bool flag2 = c == '.';
                if (flag2) {
                    num = scale * 0.7f;
                    num3 -= 5f * scale;
                }
                Color color3 = (c == ':' || c == '.' || num < scale) ? color2 : color;
                float num4 = (((c == ':' || c == '.') ? spacerWidth : numberWidth) + 4f) * num;
                font.DrawOutline(fontFaceSize, c.ToString(), new Vector2(num2 + num4 / 2f, num3), new Vector2(0.5f, 1f), Vector2.One * num, color3, 2f, Color.Black);
                num2 += num4;
            }
        }

        static SpeedBerryTimerDisplay() {
            numberWidth = 0f;
            spacerWidth = 0f;
        }

        public float CompleteTimer;

        public const int GuiChapterHeight = 58;

        public const int GuiFileHeight = 78;

        private static float numberWidth;

        private static float spacerWidth;

        private MTexture bg;

        public float DrawLerp;

        private Wiggler wiggler;
    }
}
