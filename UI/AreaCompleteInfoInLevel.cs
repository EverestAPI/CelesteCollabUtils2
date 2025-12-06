using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.CollabUtils2.UI {
    /// <summary>
    /// A standalone entity displaying the area complete stats directly in a level.
    /// </summary>
    [Tracked]
    public class AreaCompleteInfoInLevel : Entity {
        // this field is added by Everest, so it isn't publicized
        private static FieldInfo areaCompleteVersionFull = typeof(AreaComplete).GetField("versionFull", BindingFlags.NonPublic | BindingFlags.Static);

        private static ILHook versionNumberAndVariantsHook;

        private static bool isCollabEndscreen = false;

        private float speedrunTimerEase = 0f;

        private string speedrunTimerChapterString;
        private string speedrunTimerFileString;
        private string chapterSpeedrunText = Dialog.Get("OPTIONS_SPEEDRUN_CHAPTER") + ":";
        private string version = Celeste.Instance.Version.ToString();

        internal static void Load() {
            On.Celeste.AreaComplete.InitAreaCompleteInfoForEverest += onAreaCompleteInit;
            On.Celeste.AreaComplete.DisposeAreaCompleteInfoForEverest += onAreaCompleteDispose;
            versionNumberAndVariantsHook = new ILHook(typeof(AreaComplete).GetMethod("orig_VersionNumberAndVariants"), shiftVersionNumberAndVariantsUp);
        }

        internal static void Unload() {
            On.Celeste.AreaComplete.InitAreaCompleteInfoForEverest -= onAreaCompleteInit;
            On.Celeste.AreaComplete.DisposeAreaCompleteInfoForEverest -= onAreaCompleteDispose;
            versionNumberAndVariantsHook?.Dispose();
        }

        private static void onAreaCompleteInit(On.Celeste.AreaComplete.orig_InitAreaCompleteInfoForEverest orig, bool pieScreen) {
            orig(pieScreen);

            if (Settings.Instance.SpeedrunClock > SpeedrunType.Off) {
                string mapSID = (Engine.Scene as AreaComplete)?.Session.Area.GetSID();
                if (mapSID != null) {
                    addCollabVersionToEndscreen(mapSID);
                }
            }
        }

        private static void onAreaCompleteDispose(On.Celeste.AreaComplete.orig_DisposeAreaCompleteInfoForEverest orig) {
            orig();

            isCollabEndscreen = false;
        }

        private static void shiftVersionNumberAndVariantsUp(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(1020f))) {
                Logger.Log("CollabUtils2/AreaCompleteInfoInLevel", $"Shifting version number and variants up at {cursor.Index} in IL for AreaComplete.orig_VersionNumberAndVariants");
                cursor.EmitDelegate<Func<float, float>>(moveTextUp);
            }
        }

        private static float moveTextUp(float orig) {
            if (isCollabEndscreen) {
                // shift the text up to leave more space for the collab version.
                orig -= 32f;
            }
            return orig;
        }

        public AreaCompleteInfoInLevel() : base() {
            AddTag(Tags.HUD | Tags.FrozenUpdate);
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            AreaComplete.InitAreaCompleteInfoForEverest(pieScreen: false);

            addCollabVersionToEndscreen((scene as Level).Session.Area.GetSID());

            speedrunTimerChapterString = TimeSpan.FromTicks((scene as Level).Session.Time).ShortGameplayFormat();
            speedrunTimerFileString = Dialog.FileTime(SaveData.Instance.Time);
            SpeedrunTimerDisplay.CalculateBaseSizes();
        }

        private static void addCollabVersionToEndscreen(string levelSID) {
            // add the collab version after the Everest version
            string collabName = LobbyHelper.GetCollabNameForSID(levelSID);
            if (collabName != null) {
                string collabNameFormatted = Dialog.Clean($"endscreen_collabname_{collabName}");
                areaCompleteVersionFull.SetValue(null, areaCompleteVersionFull.GetValue(null).ToString() + $"\n{collabNameFormatted} " +
                    (Everest.Modules.Where(m => m.Metadata?.Name == collabName).FirstOrDefault()?.Metadata.Version ?? new Version(0, 0)));

                isCollabEndscreen = true;
            }
        }

        public override void Update() {
            base.Update();

            speedrunTimerEase = Calc.Approach(speedrunTimerEase, 1f, Engine.DeltaTime * 4f);
        }

        public override void Render() {
            base.Render();

            AreaComplete.Info(speedrunTimerEase, speedrunTimerChapterString, speedrunTimerFileString, chapterSpeedrunText, version);

            ActiveFont.DrawOutline(Dialog.Clean((Scene as Level).Session.Area.GetSID()),
                new Vector2(960f, 900f), new Vector2(0.5f, 0.5f), Vector2.One * 0.5f, Color.White, 2f, Color.Black);
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);

            AreaComplete.DisposeAreaCompleteInfoForEverest();
        }
    }
}
