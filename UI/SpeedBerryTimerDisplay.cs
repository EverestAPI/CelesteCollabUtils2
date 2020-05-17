using Celeste.Mod.CollabUtils2.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.UI {
    [Tracked]
    public class SpeedBerryTimerDisplay : Entity {

        public static void Load() {
            On.Celeste.TotalStrawberriesDisplay.Update += onTotalStrawberriesDisplayUpdate;
        }

        public static void Unload() {
            On.Celeste.TotalStrawberriesDisplay.Update -= onTotalStrawberriesDisplayUpdate;
        }

        private static void onTotalStrawberriesDisplayUpdate(On.Celeste.TotalStrawberriesDisplay.orig_Update orig, TotalStrawberriesDisplay self) {
            bool hasSpeedBerryTimer = self.Scene.Tracker.CountEntities<SpeedBerryTimerDisplay>() > 0;

            orig(self);

            if (hasSpeedBerryTimer && CollabModule.Instance.Settings.SpeedBerryTimerPosition == CollabSettings.SpeedBerryTimerPositions.TopLeft
                && self.Visible) {

                float expectedY = 206f;
                if (!self.SceneAs<Level>().TimerHidden) {
                    if (Settings.Instance.SpeedrunClock == SpeedrunType.Chapter) {
                        expectedY += 58f;
                    } else if (Settings.Instance.SpeedrunClock == SpeedrunType.File) {
                        expectedY += 78f;
                    }
                }
                self.Y = expectedY;
            }
        }

        private const float targetTimeScale = 0.7f;
        private const float lineSeparationFactor = 0.8f;

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

        private Vector2 offscreenPosition;
        private Vector2 onscreenPosition;

        private Wiggler wiggler;
        private Tween tween;

        private MTexture bg = GFX.Gui["CollabUtils2/extendedStrawberryCountBG"];

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
            calculateBaseSizes();
            Add(wiggler = Wiggler.Create(0.5f, 4f, null, false, false));
            TrackedBerry = berry;
            timerEnded = false;
            fadeTime = 3f;
            Get<Tween>()?.RemoveSelf();
            tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 0.6f, true);
            tween.OnUpdate = delegate (Tween t) {
                Position = Vector2.Lerp(offscreenPosition, onscreenPosition, t.Eased);
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
                { "Gold", ActiveFont.Measure(Dialog.Clean("collabutils2_speedberry_gold") + " ") * targetTimeScale},
                { "Silver", ActiveFont.Measure(Dialog.Clean("collabutils2_speedberry_silver") + " ") * targetTimeScale},
                { "Bronze", ActiveFont.Measure(Dialog.Clean("collabutils2_speedberry_bronze") + " ") * targetTimeScale}
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

            if (CollabModule.Instance.Settings.SpeedBerryTimerPosition == CollabSettings.SpeedBerryTimerPositions.TopCenter) {
                offscreenPosition = new Vector2(Engine.Width / 2f, -81f);
                onscreenPosition = new Vector2(Engine.Width / 2f, 89f);
            } else if (Settings.Instance.SpeedrunClock == SpeedrunType.Off) {
                offscreenPosition = new Vector2(-400f, 100f);
                onscreenPosition = new Vector2(32f, 100f);
            } else if (Settings.Instance.SpeedrunClock == SpeedrunType.Chapter) {
                offscreenPosition = new Vector2(-400f, 160f);
                onscreenPosition = new Vector2(32f, 160f);
            } else {
                offscreenPosition = new Vector2(-400f, 180f);
                onscreenPosition = new Vector2(32f, 180f);
            }
            if (!tween.Active) {
                Position = onscreenPosition;
            }

            base.Update();
        }

        public void EndTimer() {
            if (!timerEnded) {
                timerEnded = true;
                fadeTime = 5f;
                Get<Tween>()?.RemoveSelf();
                tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeInOut, 1.4f);
                tween.OnUpdate = delegate (Tween t) {
                    Position = Vector2.Lerp(onscreenPosition, offscreenPosition, t.Eased);
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
                Vector2 rankTimeSize = new Vector2(getTimeWidth(rankTimeString), numberHeight) * targetTimeScale;

                if (CollabModule.Instance.Settings.SpeedBerryTimerPosition == CollabSettings.SpeedBerryTimerPositions.TopCenter) {
                    // Current time
                    drawTime(new Vector2(Position.X - (currentTimeSize.X / 2), Y + currentTimeSize.Y * lineSeparationFactor),
                        currentTimeString, rankColors[nextRankName], 1f + wiggler.Value * 0.15f, 1f);
                    float fontOffset = Dialog.Language.Font.Face != "Renogare" ? -2f * targetTimeScale : 0f;

                    if (nextRankName != "None") {
                        // draw next best rank time
                        Vector2 totalSize = rankTimeSize + rankMeasurements[nextRankName].X * Vector2.UnitX;
                        drawTime(new Vector2(Position.X - (totalSize.X / 2) + rankMeasurements[nextRankName].X, Y),
                            rankTimeString, rankColors[nextRankName], targetTimeScale, 1f);
                        ActiveFont.DrawOutline(Dialog.Clean($"collabutils2_speedberry_{nextRankName}"),
                            new Vector2(Position.X - (totalSize.X / 2), Y + fontOffset),
                            new Vector2(0f, 1f), Vector2.One * targetTimeScale, rankColors[nextRankName], 2f, Color.Black);
                    } else {
                        // draw "time ran out!" text
                        ActiveFont.DrawOutline(Dialog.Clean($"collabutils2_speedberry_timeranout"),
                            new Vector2(Position.X, Y + fontOffset),
                            new Vector2(0.5f, 1f), Vector2.One * targetTimeScale, rankColors[nextRankName], 2f, Color.Black);
                    }
                } else {
                    // Current time
                    bg.Draw(new Vector2(Position.X - 568 + currentTimeSize.X, Y + currentTimeSize.Y * lineSeparationFactor - 45));
                    drawTime(new Vector2(Position.X, Y + currentTimeSize.Y * lineSeparationFactor),
                        currentTimeString, rankColors[nextRankName], 1f + wiggler.Value * 0.15f, 1f);

                    float fontOffset = Dialog.Language.Font.Face != "Renogare" ? -2f : 0f;

                    if (nextRankName != "None") {
                        // draw next best rank time
                        bg.Draw(new Vector2(Position.X - 392 + rankTimeSize.X + rankMeasurements[nextRankName].X, Y - 32), Vector2.Zero, Color.White, 0.7f);
                        drawTime(new Vector2(Position.X + rankMeasurements[nextRankName].X + 3, Y),
                            rankTimeString, rankColors[nextRankName], targetTimeScale, 1f);
                        ActiveFont.DrawOutline(Dialog.Clean($"collabutils2_speedberry_{nextRankName}"),
                            new Vector2(Position.X + 3, Y + fontOffset),
                            new Vector2(0f, 1f), Vector2.One * targetTimeScale, rankColors[nextRankName], 2f, Color.Black);
                    } else {
                        // draw "time ran out!" text
                        bg.Draw(new Vector2(Position.X - 392 + ActiveFont.Measure(Dialog.Clean($"collabutils2_speedberry_timeranout")).X * targetTimeScale,
                            Y - 32), Vector2.Zero, Color.White, 0.7f);

                        ActiveFont.DrawOutline(Dialog.Clean($"collabutils2_speedberry_timeranout"),
                            new Vector2(Position.X + 3, Y + fontOffset),
                            new Vector2(0f, 1f), Vector2.One * targetTimeScale, rankColors[nextRankName], 2f, Color.Black);
                    }
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
