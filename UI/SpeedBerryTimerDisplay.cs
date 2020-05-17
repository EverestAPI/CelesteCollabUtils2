using Celeste.Mod.CollabUtils2.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.UI {
    public class SpeedBerryTimerDisplay : Entity {

        public SpeedBerry TrackedBerry;

        private long startChapterTimer = -1;
        private long endChapterTimer = -1;

        // measurements
        private float numberWidth;
        private float spacerWidth;
        private float numberHeight;

        private Dictionary<string, Vector2> rankMeasurements;

        // draw state
        private float fadeTime;
        private bool timerEnded;
        private float drawLerp;
        private bool tweenStarted;

        private Wiggler wiggler;
        private Tween tween;

        private static Dictionary<string, Color> rankColors = new Dictionary<string, Color>() {
            { "Bronze", Calc.HexToColor("cd7f32") },
            { "Silver", Color.Silver },
            { "Gold", Color.Gold },
            { "None", Color.OrangeRed }
        };

        public SpeedBerryTimerDisplay(SpeedBerry berry) {
            drawLerp = 0f;
            Tag = (Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate);
            Depth = -100;
            Position = new Vector2(Engine.Width / 2f, -66f);
            calculateBaseSizes();
            Add(wiggler = Wiggler.Create(0.5f, 4f, null, false, false));
            TrackedBerry = berry;

            timerEnded = false;
            fadeTime = 3f;
            Vector2 start = Position;
            Vector2 end = new Vector2(Position.X, 104f);
            Get<Tween>()?.RemoveSelf();
            tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 0.6f, true);
            tween.OnUpdate = delegate (Tween t) {
                Position = Vector2.Lerp(start, end, t.Eased);
            };
            Add(tween);
        }

        public long GetSpentTime() {
            if (startChapterTimer == -1) {
                return 0;
            } else if (endChapterTimer == -1) {
                return SceneAs<Level>().Session.Time - startChapterTimer;
            }
            return endChapterTimer - startChapterTimer;
        }

        public void StartTimer() {
            if (startChapterTimer == -1) {
                startChapterTimer = SceneAs<Level>().Session.Time;
            }
        }

        private void calculateBaseSizes() {
            // compute the max size of a digit and separators in the English font, for the timer part.
            PixelFont font = Dialog.Languages["english"].Font;
            float fontFaceSize = Dialog.Languages["english"].FontFaceSize;
            PixelFontSize pixelFontSize = font.Get(fontFaceSize);
            for (int i = 0; i < 10; i++) {
                float digitWidth = pixelFontSize.Measure(i.ToString()).X;
                if (digitWidth > numberWidth) {
                    numberWidth = digitWidth;
                }
            }
            spacerWidth = pixelFontSize.Measure('.').X;
            numberHeight = pixelFontSize.Measure("0:.").Y;

            // measure the ranks in the font for the current language.
            rankMeasurements = new Dictionary<string, Vector2>() {
                { "Gold", ActiveFont.Measure(Dialog.Clean("collabutils2_speedberry_gold") + " ")},
                { "Silver", ActiveFont.Measure(Dialog.Clean("collabutils2_speedberry_silver") + " ")},
                { "Bronze", ActiveFont.Measure(Dialog.Clean("collabutils2_speedberry_bronze") + " ")}
            };
        }

        public override void Update() {
            drawLerp = Calc.Approach(drawLerp, 1f, Engine.DeltaTime * 4f);
            if (timerEnded) {
                fadeTime -= Engine.DeltaTime;
                if (fadeTime <= 0f) {
                    SceneAs<Level>().Remove(this);
                } else if (!tweenStarted && fadeTime <= 1.5f) {
                    tween.Start();
                    tweenStarted = true;
                }
            }

            base.Update();
        }

        public void EndTimer() {
            if (!timerEnded) {
                timerEnded = true;
                fadeTime = 5f;
                Vector2 start = Position;
                Vector2 end = new Vector2(Position.X, -66f);
                Get<Tween>()?.RemoveSelf();
                tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 1.4f);
                tween.OnUpdate = delegate (Tween t) {
                    Position = Vector2.Lerp(start, end, t.Eased);
                };
                Add(tween);
                endChapterTimer = SceneAs<Level>().Session.Time;
                wiggler.Start();
            }
        }

        public string GetNextRank(out float nextRankTime) {
            float currentTime = (float) TimeSpan.FromTicks(GetSpentTime()).TotalSeconds;
            string nextRankName;
            if (currentTime < TrackedBerry.GoldTime) {
                nextRankTime = TrackedBerry.GoldTime;
                nextRankName = "Gold";
            } else if (currentTime < TrackedBerry.SilverTime) {
                nextRankTime = TrackedBerry.SilverTime;
                nextRankName = "Silver";
            } else if (currentTime < TrackedBerry.BronzeTime) {
                nextRankTime = TrackedBerry.BronzeTime;
                nextRankName = "Bronze";
            } else {
                // time ran out
                nextRankTime = 0;
                nextRankName = "None";
            }
            return nextRankName;
        }

        public override void Render() {
            if (!(drawLerp <= 0f) && fadeTime > 0f) {
                // get next best rank time
                string nextRankName = GetNextRank(out float nextRankTime);
                string currentTimeString = TimeSpan.FromTicks(GetSpentTime()).ShortGameplayFormat();
                string rankTimeString = TimeSpan.FromSeconds(nextRankTime).ShortGameplayFormat();

                float scale = 1f + wiggler.Value * 0.15f;
                Vector2 currentTimeSize = new Vector2(getTimeWidth(currentTimeString, scale), numberHeight);
                Vector2 rankTimeSize = new Vector2(getTimeWidth(rankTimeString), numberHeight);

                // Current time
                drawTime(new Vector2(Position.X - (currentTimeSize.X / 2), Y), currentTimeString, rankColors[nextRankName], 1f + wiggler.Value * 0.15f, 1f);
                float fontOffset = Dialog.Language.Font.Face != "Renogare" ? -2f : 0f;

                if (nextRankName != "None") {
                    // draw next best rank time
                    Vector2 totalSize = rankTimeSize + rankMeasurements[nextRankName].X * Vector2.UnitX;
                    drawTime(new Vector2(Position.X - (totalSize.X / 2) + rankMeasurements[nextRankName].X, Y + totalSize.Y),
                        rankTimeString, rankColors[nextRankName], 1f, 1f);
                    ActiveFont.Draw(Dialog.Clean($"collabutils2_speedberry_{nextRankName}"),
                        new Vector2(Position.X - (totalSize.X / 2), Y + totalSize.Y + fontOffset),
                        new Vector2(0f, 1f), Vector2.One, rankColors[nextRankName]);
                } else {
                    // draw "time ran out!" text
                    ActiveFont.Draw(Dialog.Clean($"collabutils2_speedberry_timeranout"),
                        new Vector2(Position.X, Y + rankTimeSize.Y + fontOffset),
                        new Vector2(0.5f, 1f), Vector2.One, rankColors[nextRankName]);
                }
            }
        }

        private float getTimeWidth(string timeString, float scale = 1f) {
            float currentScale = scale;
            float currentWidth = 0f;
            foreach (char c in timeString) {
                if (c == '.') {
                    currentScale = scale * 0.7f;
                }
                currentWidth += (((c == ':' || c == '.') ? spacerWidth : numberWidth) + 4f) * currentScale;
            }
            return currentWidth;
        }

        private void drawTime(Vector2 position, string timeString, Color color, float scale = 1f, float alpha = 1f) {
            PixelFont font = Dialog.Languages["english"].Font;
            float fontFaceSize = Dialog.Languages["english"].FontFaceSize;
            float currentScale = scale;
            float currentX = position.X;
            float currentY = position.Y;
            color = color * alpha;
            Color colorDoubleAlpha = color * alpha;

            foreach (char c in timeString) {
                bool flag2 = c == '.';
                if (flag2) {
                    currentScale = scale * 0.7f;
                    currentY -= 5f * scale;
                }
                Color colorToUse = (c == ':' || c == '.' || currentScale < scale) ? colorDoubleAlpha : color;
                float advance = (((c == ':' || c == '.') ? spacerWidth : numberWidth) + 4f) * currentScale;
                font.DrawOutline(fontFaceSize, c.ToString(), new Vector2(currentX + advance / 2, currentY), new Vector2(0.5f, 1f), Vector2.One * currentScale, colorToUse, 2f, Color.Black);
                currentX += advance;
            }
        }
    }
}
