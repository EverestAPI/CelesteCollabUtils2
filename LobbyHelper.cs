using Celeste.Mod.CollabUtils2.UI;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.CollabUtils2 {
    class LobbyHelper {

        private static bool unpauseTimerOnNextAction = false;

        private static ILHook hookOnOuiFileSelectRender;
        private static ILHook hookOnOuiJournalPoemLines;

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
        /// Returns the SID of the lobby corresponding to this level set.
        /// </summary>
        /// <param name="levelSet">The level set name</param>
        /// <returns>The SID of the lobby for this level set, or null if the given level set does not belong to a collab or has no matching lobby.</returns>
        public static string GetLobbyForLevelSet(string levelSet) {
            if (levelSet.StartsWith("SpringCollab2020/")) {
                // build the expected lobby name (SpringCollab2020/1-Beginner => SpringCollab2020/0-Lobbies/1-Beginner) and check it exists before returning it.
                string expectedLobbyName = "SpringCollab2020/0-Lobbies/" + levelSet.Substring("SpringCollab2020/".Length);
                if (AreaData.Get(expectedLobbyName) != null) {
                    return expectedLobbyName;
                }
            }
            return null;
        }

        /// <summary>
        /// Check if the given SID matches a collab heart side level.
        /// </summary>
        /// <param name="sid">The SID for a map</param>
        /// <returns>true if this is a collab heart side, false otherwise.</returns>
        public static bool IsHeartSide(string sid) {
            return sid.StartsWith("SpringCollab2020/") && sid.EndsWith("/ZZ-HeartSide");
        }

        public static void Load() {
            On.Celeste.Level.LoadLevel += onLoadLevel;
            On.Celeste.Player.Update += onPlayerUpdate;
            On.Celeste.SaveData.RegisterHeartGem += onRegisterHeartGem;
            On.Celeste.SaveData.RegisterPoemEntry += onRegisterPoemEntry;
            On.Celeste.SaveData.RegisterCompletion += onRegisterCompletion;
            On.Celeste.SaveData.AfterInitialize += onSaveDataAfterInitialize;
            Everest.Events.Journal.OnEnter += onJournalEnter;
            On.Celeste.OuiFileSelectSlot.Show += onOuiFileSelectSlotShow;

            hookOnOuiFileSelectRender = new ILHook(typeof(OuiFileSelectSlot).GetMethod("orig_Render"), modSelectSlotLevelSetDisplayName);
            hookOnOuiJournalPoemLines = new ILHook(typeof(OuiJournalPoem).GetNestedType("PoemLine", BindingFlags.NonPublic).GetMethod("Render"), modJournalPoemHeartColors);
        }

        public static void Unload() {
            On.Celeste.Level.LoadLevel -= onLoadLevel;
            On.Celeste.Player.Update -= onPlayerUpdate;
            On.Celeste.SaveData.RegisterHeartGem -= onRegisterHeartGem;
            On.Celeste.SaveData.RegisterPoemEntry -= onRegisterPoemEntry;
            On.Celeste.SaveData.RegisterCompletion -= onRegisterCompletion;
            On.Celeste.SaveData.AfterInitialize -= onSaveDataAfterInitialize;
            Everest.Events.Journal.OnEnter -= onJournalEnter;
            On.Celeste.OuiFileSelectSlot.Show -= onOuiFileSelectSlotShow;

            hookOnOuiFileSelectRender?.Dispose();
            hookOnOuiJournalPoemLines?.Dispose();
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

            // be sure that all lobbies are unlocked.
            LevelSetStats stats = self.GetLevelSetStatsFor("SpringCollab2020/0-Lobbies");
            if (stats != null) {
                stats.UnlockedAreas = stats.Areas.Count - 1;
            }
        }

        private static void onJournalEnter(OuiJournal journal, Oui from) {
            if (SaveData.Instance.GetLevelSet() == "SpringCollab2020/0-Lobbies") {
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
            orig(self);

            if (self.SaveData?.LevelSet == "SpringCollab2020/0-Lobbies") {
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

                // aggregate all stats for SpringCollab2020 level sets.
                foreach (LevelSetStats stats in self.SaveData.LevelSets) {
                    if (stats.Name.StartsWith("SpringCollab2020/")) {
                        totalStrawberries += stats.TotalStrawberries;
                        totalGoldenStrawberries += stats.TotalGoldenStrawberries;
                        totalHeartGems += stats.TotalHeartGems;
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
                    if (self.SaveData?.LevelSet == "SpringCollab2020/0-Lobbies") {
                        return self.SaveData.LevelSet.DialogKeyify();
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
                    if (SaveData.Instance?.LevelSet == "SpringCollab2020/0-Lobbies") {
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
