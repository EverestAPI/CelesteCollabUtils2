using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using System;
using System.Reflection;

namespace Celeste.Mod.CollabUtils2.UI {
    class SpeedBerryPBInChapterPanel {
        private static SpeedBerryPBDisplay speedBerryPBDisplay;
        private static Vector2 speedBerryPBOffset;

        public static void Load() {
            On.Celeste.OuiChapterPanel.ctor += modOuiChapterPanelConstructor;
            IL.Celeste.OuiChapterPanel.Render += modOuiChapterPanelRender;
            On.Celeste.OuiChapterPanel.UpdateStats += modOuiChapterPanelUpdateStats;
            IL.Celeste.OuiChapterPanel.SetStatsPosition += modOuiChapterPanelSetStatsPosition;
        }

        public static void Unload() {
            On.Celeste.OuiChapterPanel.ctor -= modOuiChapterPanelConstructor;
            IL.Celeste.OuiChapterPanel.Render -= modOuiChapterPanelRender;
            On.Celeste.OuiChapterPanel.UpdateStats -= modOuiChapterPanelUpdateStats;
            IL.Celeste.OuiChapterPanel.SetStatsPosition -= modOuiChapterPanelSetStatsPosition;
        }

        private static Color getRankColor(CollabMapDataProcessor.SpeedBerryInfo speedBerryInfo, long pb) {
            float pbSeconds = (float) TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold) {
                return Calc.HexToColor("D2B007");
            } else if (pbSeconds < speedBerryInfo.Silver) {
                return Color.Silver;
            }
            return Calc.HexToColor("B96F11");
        }

        private static string getRankIcon(CollabMapDataProcessor.SpeedBerryInfo speedBerryInfo, long pb) {
            float pbSeconds = (float) TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold) {
                return "CollabUtils2/speedberry_gold";
            } else if (pbSeconds < speedBerryInfo.Silver) {
                return "CollabUtils2/speedberry_silver";
            }
            return "CollabUtils2/speedberry_bronze";
        }

        private static void modOuiChapterPanelConstructor(On.Celeste.OuiChapterPanel.orig_ctor orig, OuiChapterPanel self) {
            orig(self);

            // add the speed berry PB display as well, but have it hidden by default
            self.Add(speedBerryPBDisplay = new SpeedBerryPBDisplay());
        }

        private static void modOuiChapterPanelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // move after the deaths counter positioning, and place ourselves after that to update speed berry PB position as well
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(DeathsCounter), "Position"))) {
                Logger.Log("CollabUtils2/SpeedBerryPBInChapterPanel", $"Injecting speed berry PB position updating at {cursor.Index} in CIL code for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("contentOffset", BindingFlags.NonPublic | BindingFlags.Instance));
                cursor.EmitDelegate<Action<Vector2>>(contentOffset => {
                    if (speedBerryPBDisplay != null) {
                        speedBerryPBDisplay.Position = contentOffset + new Vector2(0f, 170f) + speedBerryPBOffset;
                    }
                });
            }
        }

        private static void modOuiChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {
            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            if (speedBerryPBDisplay != null) {
                if (CollabMapDataProcessor.SpeedBerries.TryGetValue(self.Area.GetSID(), out CollabMapDataProcessor.SpeedBerryInfo speedBerryInfo)
                    && CollabModule.Instance.SaveData.SpeedBerryPBs.TryGetValue(self.Area.GetSID(), out long speedBerryPB)) {

                    speedBerryPBDisplay.Visible = true;
                    speedBerryPBDisplay.Icon = GFX.Gui[getRankIcon(speedBerryInfo, speedBerryPB)];
                    speedBerryPBDisplay.Color = getRankColor(speedBerryInfo, speedBerryPB);
                    speedBerryPBDisplay.Text = Dialog.Time(speedBerryPB);
                } else {
                    speedBerryPBDisplay.Visible = false;
                }
            }
        }

        private static void modOuiChapterPanelSetStatsPosition(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // this is a tricky one... in lines like this:
            // this.strawberriesOffset = this.Approach(this.strawberriesOffset, new Vector2(120f, (float)(this.deaths.Visible ? -40 : 0)), !approach);
            // we want to catch the result of (float)(this.deaths.Visible ? -40 : 0) and transform it to shift the things up if the speed berry PB is there.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchConvR4())) {
                Logger.Log("CollabUtils2/SpeedBerryPBInChapterPanel", $"Modifying strawberry/death counter positioning at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<float, float>>(position => (speedBerryPBDisplay?.Visible ?? false) ? position - 40 : position);
            }

            cursor.Index = 0;

            // we will cross 2 occurrences when deathsOffset will be set: first time with the heart, second time without.
            // the only difference is the X offset, so put the code in common.
            bool hasHeart = true;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(OuiChapterPanel), "deathsOffset"))) {
                Logger.Log("CollabUtils2/SpeedBerryPBInChapterPanel", $"Injecting speed berry PB position updating at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition (has heart = {hasHeart})");

                // bool approach
                cursor.Emit(OpCodes.Ldarg_1);
                // StrawberriesCounter strawberries
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("strawberries", BindingFlags.NonPublic | BindingFlags.Instance));
                // DeathsCounter deaths
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("deaths", BindingFlags.NonPublic | BindingFlags.Instance));
                // bool hasHeart
                cursor.Emit(hasHeart ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                // function call
                cursor.EmitDelegate<Action<bool, StrawberriesCounter, DeathsCounter, bool>>((approach, strawberries, deaths, thisHasHeart) => {
                    int shift = 0;
                    if (strawberries.Visible)
                        shift += 40;
                    if (deaths.Visible)
                        shift += 40;
                    speedBerryPBOffset = SpeedBerryPBInChapterPanel.approach(speedBerryPBOffset, new Vector2(thisHasHeart ? 150f : 0f, shift), !approach);
                });

                hasHeart = false;
            }

            cursor.Index = 0;

            // have a bigger spacing between heart and text when the speed berry PB is displayed, because it is bigger than berry / death count.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(120f) || instr.MatchLdcR4(-120f))) {
                Logger.Log("CollabUtils2/SpeedBerryPBInChapterPanel", $"Modifying column spacing at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<float, float>>(orig => {
                    if (speedBerryPBDisplay?.Visible ?? false) {
                        return orig + 30f * Math.Sign(orig);
                    }
                    return orig;
                });
            }
        }

        // vanilla method copypaste
        private static Vector2 approach(Vector2 from, Vector2 to, bool snap) {
            if (snap)
                return to;
            return from += (to - from) * (1f - (float) Math.Pow(0.0010000000474974513, Engine.DeltaTime));
        }
    }
}
