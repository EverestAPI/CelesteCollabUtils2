using Celeste.Mod.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    class LobbyHelper {

        private static bool unpauseTimerOnNextAction = false;

        /// <summary>
        /// Returns the level set the given lobby SID is associated to, or null if the SID given is not a lobby.
        /// </summary>
        /// <param name="sid">The SID for a map</param>
        /// <returns>The level set name for this lobby, or null if the SID given is not a lobby</returns>
        public static string GetLobbyLevelSet(string sid) {
            if (sid.StartsWith("SpringCollab2020/0-Lobbies/")) {
                return "SpringCollab2020/" + sid.Substring("SpringCollab2020/0-Lobbies/".Length);
            }
            return null;
        }

        /// <summary>
        /// Returns true if the given level set is a hidden level set from a collab.
        /// </summary>
        public static bool IsCollabLevelSet(string levelSet) {
            return levelSet.StartsWith("SpringCollab2020/") && levelSet != "SpringCollab2020/0-Lobbies";
        }

        private static ILHook hookOnLevelSetSwitch;

        public static void Load() {
            // timer pausing when returning to lobby
            On.Celeste.Level.LoadLevel += onLoadLevel;
            On.Celeste.Player.Update += onPlayerUpdate;

            // hiding collab maps from chapter select
            hookOnLevelSetSwitch = HookHelper.HookCoroutine("Celeste.Mod.UI.OuiHelper_ChapterSelect_LevelSet", "Enter", modLevelSetSwitch);
            IL.Celeste.Mod.UI.OuiMapSearch.ReloadItems += modMapSearch;
            IL.Celeste.Mod.UI.OuiMapList.ReloadItems += modMapListReloadItems;
            IL.Celeste.Mod.UI.OuiMapList.CreateMenu += modMapListCreateMenu;
        }

        public static void Unload() {
            On.Celeste.Level.LoadLevel -= onLoadLevel;
            On.Celeste.Player.Update -= onPlayerUpdate;

            hookOnLevelSetSwitch?.Dispose();
            IL.Celeste.Mod.UI.OuiMapSearch.ReloadItems -= modMapSearch;
            IL.Celeste.Mod.UI.OuiMapList.ReloadItems -= modMapListReloadItems;
            IL.Celeste.Mod.UI.OuiMapList.CreateMenu -= modMapListCreateMenu;
        }

        public static void OnSessionCreated() {
            Session session = SaveData.Instance.CurrentSession_Safe;
            string levelSet = GetLobbyLevelSet(session.Area.GetSID());
            if (levelSet != null) {
                // set session flags for each completed map in the level set.
                // this will allow, for example, stylegrounds to get activated after completing a map.
                foreach (string mapName in SaveData.Instance.GetLevelSetStatsFor(levelSet).Areas
                    .Where(area => area.Modes[0].HeartGem)
                    .Select(area => area.GetSID().Substring(levelSet.Length + 1))) {

                    session.SetFlag($"CollabUtils2_MapCompleted_{mapName}");
                }
            }
        }

        private static void onLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
            orig(self, playerIntro, isFromLoader);

            DynData<Session> sessionData = new DynData<Session>(self.Session);
            if (sessionData.Data.ContainsKey("pauseTimerUntilAction") && sessionData.Get<bool>("pauseTimerUntilAction")) {
                sessionData["pauseTimerUntilAction"] = false;
                self.TimerStopped = true;
                unpauseTimerOnNextAction = true;
            }
        }

        private static void onPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
            orig(self);

            if (unpauseTimerOnNextAction && self.InControl
                && (Input.MoveX != 0 || Input.MoveY != 0 || Input.Grab.Check || Input.Jump.Check || Input.Dash.Check)) {

                self.SceneAs<Level>().TimerStopped = false;
                unpauseTimerOnNextAction = false;
            }
        }

        private static void modLevelSetSwitch(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // target check: areaData.GetLevelSet() != levelSet
            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdloc(6), // AreaData getting considered
                instr => instr.MatchCall(typeof(AreaDataExt), "GetLevelSet"),
                instr => instr.MatchLdloc(3), // current level set
                instr => instr.MatchCall<string>("op_Inequality"))) {

                Logger.Log("CollabUtils2/LobbyHelper", $"Making chapter select skip hidden collab level sets at {cursor.Index} in IL for OuiHelper_ChapterSelect_LevelSet.Enter");

                // becomes: areaData.GetLevelSet() != levelSet && !IsCollabLevelSet(areaData.GetLevelSet())
                cursor.Emit(OpCodes.Ldloc_S, (byte) 6);
                cursor.EmitDelegate<Func<bool, AreaData, bool>>((orig, areaData) =>
                    orig && !IsCollabLevelSet(areaData.GetLevelSet()));
            }
        }

        private static void modMapSearch(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // target check: area.HasMode(AreaMode.Normal)
            // area is actually stored in a "DisplayClass" nested type, explaining the extra ldfld "area".
            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdloc(13),
                instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name == "area",
                instr => instr.MatchLdcI4(0),
                instr => instr.MatchCallvirt<AreaData>("HasMode"))) {

                Logger.Log("CollabUtils2/LobbyHelper", $"Hiding collab entries in map search at {cursor.Index} in IL for OuiMapSearch.ReloadItems");

                // becomes: area.HasMode(AreaMode.Normal) && !IsCollabLevelSet(area.GetLevelSet())
                cursor.Emit(OpCodes.Ldloc_S, (byte) 13);
                cursor.Emit(OpCodes.Ldfld, cursor.Instrs[cursor.Index - 4].Operand as FieldReference);
                cursor.EmitDelegate<Func<bool, AreaData, bool>>((orig, areaData) =>
                    orig && !IsCollabLevelSet(areaData.GetLevelSet()));
            }
        }

        private static void modMapListReloadItems(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // target check: area.HasMode((AreaMode)side)
            // area is actually stored in a "DisplayClass" nested type, explaining the extra ldfld "area".
            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdloc(12),
                instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name == "area",
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdfld<OuiMapList>("side"),
                instr => instr.MatchCallvirt<AreaData>("HasMode"))) {

                Logger.Log("CollabUtils2/LobbyHelper", $"Hiding collab entries from map list at {cursor.Index} in IL for OuiMapList.ReloadItems");

                // becomes: area.HasMode((AreaMode)side) && !IsCollabLevelSet(area.GetLevelSet())
                cursor.Emit(OpCodes.Ldloc_S, (byte) 12);
                cursor.Emit(OpCodes.Ldfld, cursor.Instrs[cursor.Index - 5].Operand as FieldReference);
                cursor.EmitDelegate<Func<bool, AreaData, bool>>((orig, areaData) =>
                    orig && !IsCollabLevelSet(areaData.GetLevelSet()));
            }
        }

        private static void modMapListCreateMenu(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // target check: levelSet == "Celeste"
            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdloc(1),
                instr => instr.MatchLdstr("Celeste"),
                instr => instr.MatchCall<string>("op_Equality"))) {

                Logger.Log("CollabUtils2/LobbyHelper", $"Hiding collab entries in level set list at {cursor.Index} in IL for OuiMapList.ReloadItems");

                // becomes: levelSet == "Celeste" || IsCollabLevelSet(levelSet)
                cursor.Emit(OpCodes.Ldloc_1);
                cursor.EmitDelegate<Func<bool, string, bool>>((orig, levelSet) =>
                    orig || IsCollabLevelSet(levelSet));
            }
        }
    }
}
