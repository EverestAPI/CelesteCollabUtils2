using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.CollabUtils2 {
    static class LobbyHelper {

        private static bool unpauseTimerOnNextAction = false;

        private static ILHook hookOnOuiFileSelectRender;
        private static ILHook hookOnOuiJournalPoemLines;
        private static ILHook hookOnLevelSetSwitch;

        private static HashSet<string> collabNames = new HashSet<string>();

        internal static void OnInitialize() {
            foreach (ModAsset asset in
                Everest.Content.Mods
                .Select(mod => mod.Map.TryGetValue("CollabUtils2CollabID", out ModAsset asset) ? asset : null)
                .Where(asset => asset != null)
            ) {
                LoadCollabIDFile(asset);
            }
        }

        internal static void LoadCollabIDFile(ModAsset asset) {
            string fileContents;
            using (StreamReader reader = new StreamReader(asset.Stream)) {
                fileContents = reader.ReadToEnd();
            }
            Logger.Log(LogLevel.Info, "CollabUtils2/LobbyHelper", $"Registered new collab ID: {fileContents.Trim()}");
            collabNames.Add(fileContents.Trim());
        }

        /// <summary>
        /// Returns the level set the given lobby SID is associated to, or null if the SID given is not a lobby.
        /// </summary>
        /// <param name="sid">The SID for a map</param>
        /// <returns>The level set name for this lobby, or null if the SID given is not a lobby</returns>
        public static string GetLobbyLevelSet(string sid) {
            string collab = collabNames.FirstOrDefault(collabName => sid.StartsWith($"{collabName}/0-Lobbies/") && sid != $"{collabName}/0-Lobbies/0-Prologue");
            if (collab != null) {
                return $"{collab}/{sid.Substring($"{collab}/0-Lobbies/".Length)}";
            }
            return null;
        }

        /// <summary>
        /// Returns true if the given level set is a hidden level set from a collab.
        /// </summary>
        public static bool IsCollabLevelSet(string levelSet) {
            return collabNames.Any(collabName => levelSet.StartsWith($"{collabName}/") && levelSet != $"{collabName}/0-Lobbies");
        }

        /// <summary>
        /// Returns the SID of the lobby corresponding to this level set.
        /// </summary>
        /// <param name="levelSet">The level set name</param>
        /// <returns>The SID of the lobby for this level set, or null if the given level set does not belong to a collab or has no matching lobby.</returns>
        public static string GetLobbyForLevelSet(string levelSet) {
            string collab = collabNames.FirstOrDefault(collabName => levelSet.StartsWith($"{collabName}/"));
            if (collab != null) {
                // build the expected lobby name (SpringCollab2020/1-Beginner => SpringCollab2020/0-Lobbies/1-Beginner) and check it exists before returning it.
                string expectedLobbyName = $"{collab}/0-Lobbies/{levelSet.Substring($"{collab}/".Length)}";
                if (AreaData.Get(expectedLobbyName) != null) {
                    return expectedLobbyName;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the SID of the lobby corresponding to this gym.
        /// </summary>
        /// <param name="gymSID">The gym SID</param>
        /// <returns>The SID of the lobby for this gym, or null if the given SID does not belong to a collab or has no matching lobby.</returns>
        public static string GetLobbyForGym(string gymSID) {
            if (collabNames.Any(collabName => gymSID.StartsWith($"{collabName}/0-Gyms/"))) {
                // build the expected lobby name (SpringCollab2020/0-Gyms/1-Beginner => SpringCollab2020/0-Lobbies/1-Beginner) and check it exists before returning it.
                string expectedLobbyName = gymSID.Replace("/0-Gyms/", "/0-Lobbies/");
                if (AreaData.Get(expectedLobbyName) != null) {
                    return expectedLobbyName;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the name of the collab the level with the given SID is part of.
        /// </summary>
        /// <param name="sid">A map SID</param>
        /// <returns>The name of the collab the map is part of, or null if it is a non-collab map</returns>
        public static string GetCollabNameForSID(string sid) {
            return collabNames.FirstOrDefault(collabName => sid.StartsWith($"{collabName}/"));
        }

        /// <summary>
        /// Check if the given SID matches a collab heart side level.
        /// </summary>
        /// <param name="sid">The SID for a map</param>
        /// <returns>true if this is a collab heart side, false otherwise.</returns>
        public static bool IsHeartSide(string sid) {
            return collabNames.Any(collabName => sid.StartsWith($"{collabName}/")) && sid.EndsWith("/ZZ-HeartSide");
        }

        public static void Load() {
            // timer pausing when returning to lobby
            On.Celeste.Level.LoadLevel += onLoadLevel;
            On.Celeste.Player.Update += onPlayerUpdate;

            // hiding collab maps from chapter select
            hookOnLevelSetSwitch = HookHelper.HookCoroutine("Celeste.Mod.UI.OuiHelper_ChapterSelect_LevelSet", "Enter", modLevelSetSwitch);
            IL.Celeste.Mod.UI.OuiMapSearch.ReloadItems += modMapSearch;
            IL.Celeste.Mod.UI.OuiMapList.ReloadItems += modMapListReloadItems;
            IL.Celeste.Mod.UI.OuiMapList.CreateMenu += modMapListCreateMenu;
            IL.Celeste.Mod.UI.OuiFileSelectSlotLevelSetPicker.changeStartingLevelSet += modFileSelectChangeStartingLevelSet;

            On.Celeste.SaveData.RegisterHeartGem += onRegisterHeartGem;
            On.Celeste.SaveData.RegisterPoemEntry += onRegisterPoemEntry;
            On.Celeste.SaveData.RegisterCompletion += onRegisterCompletion;
            On.Celeste.SaveData.AfterInitialize += onSaveDataAfterInitialize;
            On.Celeste.OuiChapterSelectIcon.AssistModeUnlockRoutine += onAssistUnlockRoutine;
            Everest.Events.Journal.OnEnter += onJournalEnter;
            On.Celeste.OuiFileSelectSlot.Show += onOuiFileSelectSlotShow;

            hookOnOuiFileSelectRender = new ILHook(typeof(OuiFileSelectSlot).GetMethod("orig_Render"), modSelectSlotLevelSetDisplayName);
            hookOnOuiJournalPoemLines = new ILHook(typeof(OuiJournalPoem).GetNestedType("PoemLine", BindingFlags.NonPublic).GetMethod("Render"), modJournalPoemHeartColors);
        }

        public static void Unload() {
            On.Celeste.Level.LoadLevel -= onLoadLevel;
            On.Celeste.Player.Update -= onPlayerUpdate;

            hookOnLevelSetSwitch?.Dispose();
            IL.Celeste.Mod.UI.OuiMapSearch.ReloadItems -= modMapSearch;
            IL.Celeste.Mod.UI.OuiMapList.ReloadItems -= modMapListReloadItems;
            IL.Celeste.Mod.UI.OuiMapList.CreateMenu -= modMapListCreateMenu;
            IL.Celeste.Mod.UI.OuiFileSelectSlotLevelSetPicker.changeStartingLevelSet -= modFileSelectChangeStartingLevelSet;

            On.Celeste.SaveData.RegisterHeartGem -= onRegisterHeartGem;
            On.Celeste.SaveData.RegisterPoemEntry -= onRegisterPoemEntry;
            On.Celeste.SaveData.RegisterCompletion -= onRegisterCompletion;
            On.Celeste.SaveData.AfterInitialize -= onSaveDataAfterInitialize;
            On.Celeste.OuiChapterSelectIcon.AssistModeUnlockRoutine -= onAssistUnlockRoutine;
            Everest.Events.Journal.OnEnter -= onJournalEnter;
            On.Celeste.OuiFileSelectSlot.Show -= onOuiFileSelectSlotShow;

            hookOnOuiFileSelectRender?.Dispose();
            hookOnOuiJournalPoemLines?.Dispose();
        }

        public static void OnSessionCreated() {
            Session session = SaveData.Instance.CurrentSession_Safe;
            string levelSet = GetLobbyLevelSet(session.Area.GetSID());
            if (levelSet != null && SaveData.Instance.GetLevelSetStatsFor(levelSet) != null) {
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

        private static void modFileSelectChangeStartingLevelSet(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // set ourselves just after the moving operation in changeStartingLevelSet.
            if (cursor.TryGotoNext(MoveType.AfterLabel,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdsfld<AreaData>("Areas"),
                instr => instr.MatchLdloc(0),
                instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference).Name == "get_Item")) {

                Logger.Log("CollabUtils2/LobbyHelper", $"Hiding collab entries in starting level set select at {cursor.Index} in IL for OuiFileSelectSlotLevelSetPicker.changeStartingLevelSet");

                cursor.Emit(OpCodes.Ldloc_0);
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.EmitDelegate<Func<int, int, int>>((id, direction) => {
                    string currentLevelSet = AreaData.Areas[id].GetLevelSet();

                    // repeat the move until the current level set isn't a collab level set anymore.
                    while (IsCollabLevelSet(currentLevelSet)) {
                        if (direction > 0) {
                            id = AreaData.Areas.FindLastIndex(area => area.GetLevelSet() == currentLevelSet) + direction;
                        } else {
                            id = AreaData.Areas.FindIndex(area => area.GetLevelSet() == currentLevelSet) + direction;
                        }

                        if (id >= AreaData.Areas.Count)
                            id = 0;
                        if (id < 0)
                            id = AreaData.Areas.Count - 1;

                        currentLevelSet = AreaData.Areas[id].GetLevelSet();
                    }

                    return id;
                });
                cursor.Emit(OpCodes.Stloc_0);
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

        private static void onRegisterHeartGem(On.Celeste.SaveData.orig_RegisterHeartGem orig, SaveData self, AreaKey area) {
            orig(self, area);

            if (IsHeartSide(area.GetSID())) {
                string lobby = GetLobbyForLevelSet(area.GetLevelSet());
                if (lobby != null) {
                    // register the heart gem for the lobby as well.
                    self.RegisterHeartGem(AreaData.Get(lobby).ToKey());
                }
            }
        }

        private static bool onRegisterPoemEntry(On.Celeste.SaveData.orig_RegisterPoemEntry orig, SaveData self, string id) {
            bool result = orig(self, id);

            AreaKey currentArea = (Engine.Scene as Level)?.Session?.Area ?? AreaKey.Default;
            if (IsHeartSide(currentArea.GetSID())) {
                string lobby = GetLobbyForLevelSet(currentArea.GetLevelSet());
                if (lobby != null) {
                    // register the poem for the lobby level set as well.
                    List<string> levelSetPoem = self.GetLevelSetStatsFor(AreaData.Get(lobby).GetLevelSet()).Poem;
                    if (!levelSetPoem.Contains(id)) {
                        levelSetPoem.Add(id);
                    }
                }
            }

            return result;
        }

        private static void onRegisterCompletion(On.Celeste.SaveData.orig_RegisterCompletion orig, SaveData self, Session session) {
            orig(self, session);

            AreaKey currentArea = session.Area;
            if (IsHeartSide(currentArea.GetSID())) {
                string lobby = GetLobbyForLevelSet(currentArea.GetLevelSet());
                if (lobby != null) {
                    // completing the heart side should also complete the lobby.
                    AreaModeStats areaModeStats = SaveData.Instance.Areas_Safe[AreaData.Get(lobby).ID].Modes[0];
                    areaModeStats.Completed = true;
                }
            }
        }

        private static void onSaveDataAfterInitialize(On.Celeste.SaveData.orig_AfterInitialize orig, SaveData self) {
            orig(self);

            foreach (string collabName in collabNames) {
                // be sure that all lobbies are unlocked.
                LevelSetStats stats = self.GetLevelSetStatsFor($"{collabName}/0-Lobbies");
                if (stats != null && (stats.UnlockedAreas > 0 || AreaData.Get($"{collabName}/0-Lobbies/0-Prologue") == null)) { // we at least completed Prologue.
                    stats.UnlockedAreas = stats.Areas.Count - 1;
                }
            }

            if (self.CurrentSession_Safe != null && !self.CurrentSession_Safe.InArea) {
                // we aren't in a level; check if we have a hidden level set selected (this should only happen with Alt-F4).
                string lobby = GetLobbyForLevelSet(self.LastArea_Safe.GetLevelSet());
                if (lobby != null) {
                    // we are! we should change the selected level to the matching lobby instead.
                    self.LastArea_Safe = AreaData.Get(lobby).ToKey();
                }
            }
        }

        private static IEnumerator onAssistUnlockRoutine(On.Celeste.OuiChapterSelectIcon.orig_AssistModeUnlockRoutine orig, OuiChapterSelectIcon self, Action onComplete) {
            IEnumerator origRoutine = orig(self, onComplete);
            while (origRoutine.MoveNext()) {
                yield return origRoutine.Current;
            }

            string collab = collabNames.FirstOrDefault(collabName => AreaData.Get(self.Area).GetLevelSet() == $"{collabName}/0-Lobbies");
            if (collab != null) {
                // we just assist unlocked the lobbies!
                LevelSetStats stats = SaveData.Instance.GetLevelSetStatsFor($"{collab}/0-Lobbies");
                stats.UnlockedAreas = stats.Areas.Count - 1;
                List<OuiChapterSelectIcon> icons = new DynData<OuiChapterSelect>((self.Scene as Overworld).GetUI<OuiChapterSelect>()).Get<List<OuiChapterSelectIcon>>("icons");
                icons[self.Area + 1].AssistModeUnlockable = false;
                for (int i = self.Area + 2; i <= SaveData.Instance.MaxArea; i++) {
                    icons[i].Show();
                }
            }
        }

        private static void onJournalEnter(OuiJournal journal, Oui from) {
            if (collabNames.Any(collabName => SaveData.Instance.GetLevelSet() == $"{collabName}/0-Lobbies")) {
                // customize the journal in the overworld for the collab.
                for (int i = 0; i < journal.Pages.Count; i++) {
                    if (journal.Pages[i].GetType() != typeof(OuiJournalCover) && journal.Pages[i].GetType() != typeof(OuiJournalPoem)) {
                        journal.Pages.RemoveAt(i);
                        i--;
                    }
                }

                // then, fill in the journal with our custom pages.
                journal.Pages.Insert(1, new OuiJournalCollabProgressInOverworld(journal));
            }
        }

        private static void onOuiFileSelectSlotShow(On.Celeste.OuiFileSelectSlot.orig_Show orig, OuiFileSelectSlot self) {
            // If we are currently in a collab map, display the lobby level set stats instead.
            AreaKey? savedLastArea = null;
            string collab = collabNames.FirstOrDefault(collabName => self.SaveData?.LevelSet != null && self.SaveData.LevelSet.StartsWith($"{collabName}/") && self.SaveData.LevelSet != $"{collabName}/0-Lobbies");
            if (collab != null) {
                AreaData firstMapFromCollab = AreaData.Areas.FirstOrDefault(area => area.GetLevelSet() == $"{collab}/0-Lobbies");
                if (firstMapFromCollab != null) {
                    savedLastArea = self.SaveData.LastArea_Safe;
                    self.SaveData.LastArea_Safe = firstMapFromCollab.ToKey();
                    self.Strawberries.CanWiggle = false; // prevent the strawberry collect sound from playing.
                }
            }

            orig(self);

            string collab2 = collabNames.FirstOrDefault(collabName => self.SaveData?.LevelSet == $"{collabName}/0-Lobbies");
            if (collab2 != null) {
                // recompute the stats for the collab.
                int totalStrawberries = 0;
                int totalGoldenStrawberries = 0;
                int totalHeartGems = 0;
                int totalCassettes = 0;
                int maxStrawberryCount = 0;
                int maxGoldenStrawberryCount = 0;
                int maxStrawberryCountIncludingUntracked = 0;
                int maxCassettes = 0;
                int maxCrystalHearts = 0;
                int maxCrystalHeartsExcludingCSides = 0;

                // aggregate all stats for the collab level sets.
                foreach (LevelSetStats stats in self.SaveData.LevelSets) {
                    if (stats.Name.StartsWith($"{collab2}/")) {
                        totalStrawberries += stats.TotalStrawberries;
                        totalGoldenStrawberries += stats.TotalGoldenStrawberries;
                        totalHeartGems += countTotalHeartGemsForMapsThatHaveHearts(stats);
                        totalCassettes += stats.TotalCassettes;
                        maxStrawberryCount += stats.MaxStrawberries;
                        maxGoldenStrawberryCount += stats.MaxGoldenStrawberries;
                        maxStrawberryCountIncludingUntracked += stats.MaxStrawberriesIncludingUntracked;
                        maxCassettes += stats.MaxCassettes;
                        maxCrystalHearts += stats.MaxHeartGems;
                        maxCrystalHeartsExcludingCSides += stats.MaxHeartGemsExcludingCSides;
                    }
                }

                DynData<OuiFileSelectSlot> slotData = new DynData<OuiFileSelectSlot>(self);
                slotData["totalGoldenStrawberries"] = totalGoldenStrawberries;
                slotData["totalHeartGems"] = totalHeartGems;
                slotData["totalCassettes"] = totalCassettes;
                slotData["maxStrawberryCount"] = maxStrawberryCount;
                slotData["maxGoldenStrawberryCount"] = maxGoldenStrawberryCount;
                slotData["maxStrawberryCountIncludingUntracked"] = maxStrawberryCountIncludingUntracked;
                slotData["maxCassettes"] = maxCassettes;
                slotData["maxCrystalHearts"] = maxCrystalHearts;
                slotData["maxCrystalHeartsExcludingCSides"] = maxCrystalHeartsExcludingCSides;
                slotData["summitStamp"] = false;
                slotData["farewellStamp"] = false;

                self.Strawberries.Amount = totalStrawberries;
                self.Strawberries.OutOf = maxStrawberryCount;
            }

            // Restore the last area if it was replaced at the beginning of this method.
            if (savedLastArea != null) {
                self.SaveData.LastArea_Safe = savedLastArea.Value;
            }
        }

        private static int countTotalHeartGemsForMapsThatHaveHearts(LevelSetStats levelSetStats) {
            return levelSetStats.AreasIncludingCeleste.Sum((AreaStats area) => {
                int totalHeartGems = 0;
                ModeProperties[] propertiesOfAllModes = AreaData.Get(area.GetSID())?.Mode ?? new ModeProperties[0];
                for (int i = 0; i < propertiesOfAllModes.Length && i < area.Modes.Length; i++) {
                    if (area.Modes[i].HeartGem) {
                        // the crystal heart of this map/mode was collected, so check if it has one before counting it in.
                        ModeProperties modeProperties = propertiesOfAllModes[i];
                        if (modeProperties?.MapData != null && modeProperties.MapData.Area.Mode <= AreaMode.CSide) {
                            totalHeartGems += (modeProperties.MapData.DetectedHeartGem ? 1 : 0);
                        }
                    }
                }
                return totalHeartGems;
            });
        }

        private static void modSelectSlotLevelSetDisplayName(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdfld<AreaData>("Name"),
                instr => instr.MatchLdnull())) {

                Logger.Log("CollabUtils2/LobbyHelper", $"Replacing collab display name at {cursor.Index} in IL for OuiFileSelectSlot.orig_Render");

                cursor.Index--;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<string, OuiFileSelectSlot, string>>((orig, self) => {
                    string collab = collabNames.FirstOrDefault(collabName => self.SaveData?.LevelSet.StartsWith($"{collabName}/") ?? false);
                    if (collab != null) {
                        return $"{collab.DialogKeyify()}_0_Lobbies";
                    }
                    return orig;
                });
            }
        }

        private static void modJournalPoemHeartColors(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("heartgem0"))) {
                Logger.Log("CollabUtils2/LobbyHelper", $"Modding journal poem heart colors at {cursor.Index} in IL for OuiJournalPoem.PoemLine.Render");

                // load a second parameter for the delegate: PoemLine.Text.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiJournalPoem).GetNestedType("PoemLine", BindingFlags.NonPublic).GetField("Text"));

                cursor.EmitDelegate<Func<string, string, string>>((orig, poem) => {
                    if (collabNames.Any(collabName => SaveData.Instance?.LevelSet == $"{collabName}/0-Lobbies")) {
                        foreach (AreaData area in AreaData.Areas) {
                            string levelSetName = GetLobbyLevelSet(area.GetSID());
                            if (levelSetName != null
                                && Dialog.Clean("poem_" + levelSetName + "_ZZ_HeartSide_A") == poem
                                && MTN.Journal.Has("CollabUtils2Hearts/" + levelSetName)) {

                                return "CollabUtils2Hearts/" + levelSetName;
                            }
                        }
                    }
                    return orig;
                });
            }
        }
    }
}
