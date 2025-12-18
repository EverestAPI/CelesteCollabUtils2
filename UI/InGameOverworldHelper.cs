using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.Entities;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.ModInterop;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Celeste.Mod.CollabUtils2.UI {
    public static class InGameOverworldHelper {

        private struct CreditsTag {
            private static readonly Regex ParseRegex = new Regex("^{cu2_tag(\\s+(?<key>\\w+)=\"(?<value>[^\"]+)\")*}\\s*(?<text>.*)$", RegexOptions.Compiled);

            public string Text;
            public Color? TextColor;

            public MTexture BorderTexture;
            public Color? BorderColor;

            public MTexture FillTexture;
            public Color? FillColor;

            public CreditsTag(string text) {
                Text = text;
                TextColor = null;

                BorderTexture = null;
                BorderColor = null;

                FillTexture = null;
                FillColor = null;

                Match match = ParseRegex.Match(Text);
                if (match.Success) {
                    Text = match.Groups["text"].Value;

                    CaptureCollection keys = match.Groups["key"].Captures;
                    CaptureCollection values = match.Groups["value"].Captures;

                    if (keys.Count != values.Count)
                        throw new IndexOutOfRangeException("credits tag keys and values mismatched!");

                    for (int i = 0; i < keys.Count; i++) {
                        switch (keys[i].Value) {
                            case "color":
                                TextColor = Calc.HexToColor(values[i].Value);
                                break;

                            case "borderColor":
                                BorderColor = Calc.HexToColor(values[i].Value);
                                break;
                            case "borderTexture":
                                BorderTexture = GFX.Gui[values[i].Value];
                                break;

                            case "fillColor":
                                FillColor = Calc.HexToColor(values[i].Value);
                                break;
                            case "fillTexture":
                                FillTexture = GFX.Gui[values[i].Value];
                                break;

                            default:
                                continue;
                        }
                    }
                }
            }

            public static List<CreditsTag> Parse(string dialog) {
                return dialog.Replace("{break}", "\n").Split('\n').Select(line => new CreditsTag(line.Trim())).ToList();
            }
        }

        public static bool IsOpen => overworldWrapper?.Scene == Engine.Scene;

        private static SceneWrappingEntity<Overworld> overworldWrapper;

        public static SpriteBank HeartSpriteBank;
        private static Dictionary<string, string> OverrideHeartSpriteIDs = new Dictionary<string, string>();

        private static AreaKey? lastArea;

        private static List<Hook> altSidesHelperHooks = new List<Hook>();
        private static Hook hookOnMapDataOrigLoad;

        private static Dictionary<string, Color> difficultyColors = new Dictionary<string, Color>() {
            { "beginner", Calc.HexToColor("56B3FF") },
            { "intermediate", Calc.HexToColor("FF6D81") },
            { "advanced", Calc.HexToColor("FFFF89") },
            { "expert", Calc.HexToColor("FF9E66") },
            { "grandmaster", Calc.HexToColor("DD87FF") }
        };

        private static bool presenceLock = false;

        private static Hook onReloadLevelHook;
        private static Hook onChangePresenceHook;

        private static ILHook ilSwapRoutineHook;

        internal static void Load() {
            Everest.Events.Level.OnPause += OnPause;
            On.Celeste.Audio.SetMusic += OnSetMusic;
            On.Celeste.Audio.SetAmbience += OnSetAmbience;
            On.Celeste.OuiChapterPanel.Reset += OnChapterPanelReset;
            On.Celeste.SaveData.FoundAnyCheckpoints += OnSaveDataFoundAnyCheckpoints;
            On.Celeste.OuiChapterPanel.GetModeHeight += OnChapterPanelGetModeHeight;
            On.Celeste.OuiChapterPanel.Swap += OnChapterPanelSwap;
            On.Celeste.OuiChapterPanel.DrawCheckpoint += OnChapterPanelDrawCheckpoint;
            On.Celeste.OuiJournal.Enter += OnJournalEnter;
            On.Celeste.OuiChapterPanel.UpdateStats += OnChapterPanelUpdateStats;
            IL.Celeste.OuiChapterPanel.Render += ModOuiChapterPanelRender;
            IL.Celeste.DeathsCounter.Render += ModDeathsCounterRender;
            IL.Celeste.StrawberriesCounter.Render += ModStrawberriesCounterRender;
            On.Celeste.OuiChapterPanel.Start += OnOuiChapterPanelStart;
            On.Celeste.Player.Die += OnPlayerDie;

            onReloadLevelHook = new Hook(
                typeof(AssetReloadHelper).GetMethod("ReloadLevel", new Type[0]),
                typeof(InGameOverworldHelper).GetMethod("OnReloadLevel", BindingFlags.NonPublic | BindingFlags.Static));

            IL.Celeste.OuiChapterPanel._FixTitleLength += ModFixTitleLength;
            On.Celeste.OuiMainMenu.CreateButtons += OnOuiMainMenuCreateButtons;

            onChangePresenceHook = new Hook(
                typeof(Everest.DiscordSDK).GetMethod("UpdatePresence", BindingFlags.NonPublic | BindingFlags.Instance),
                typeof(InGameOverworldHelper).GetMethod("OnDiscordChangePresence", BindingFlags.NonPublic | BindingFlags.Static));
            
            
            ilSwapRoutineHook = new ILHook(
                typeof(OuiChapterPanel).GetMethod("SwapRoutine", BindingFlags.NonPublic | BindingFlags.Instance)!.GetStateMachineTarget()!,
                ModOuiChapterPanelSwapRoutine);

            hookOnMapDataOrigLoad = new Hook(
                typeof(MapData).GetMethod("orig_Load", BindingFlags.NonPublic | BindingFlags.Instance),
                typeof(InGameOverworldHelper).GetMethod("ModMapDataLoad", BindingFlags.NonPublic | BindingFlags.Static));

            typeof(ModExports).ModInterop();
        }

        public static void Initialize() {
            if (Everest.Loader.DependencyLoaded(new EverestModuleMetadata() { Name = "AltSidesHelper", Version = new Version(1, 2, 0) })) {
                Type altSidesHelperModule = Everest.Modules.Where(m => m.GetType().FullName == "AltSidesHelper.AltSidesHelperModule").First().GetType();

                altSidesHelperHooks.Add(new Hook(
                    altSidesHelperModule.GetMethod("ResetCrystalHeart", BindingFlags.NonPublic | BindingFlags.Static),
                    typeof(InGameOverworldHelper).GetMethod(nameof(resetCrystalHeartAfterAltSidesHelper), BindingFlags.NonPublic | BindingFlags.Static)));

                altSidesHelperHooks.Add(new Hook(
                    altSidesHelperModule.GetMethod("CustomizeCrystalHeart", BindingFlags.NonPublic | BindingFlags.Static),
                    typeof(InGameOverworldHelper).GetMethod(nameof(customizeCrystalHeartAfterAltSidesHelper), BindingFlags.NonPublic | BindingFlags.Static)));
            }
        }

        internal static void Unload() {
            Everest.Events.Level.OnPause -= OnPause;
            On.Celeste.Audio.SetMusic -= OnSetMusic;
            On.Celeste.Audio.SetAmbience -= OnSetAmbience;
            On.Celeste.OuiChapterPanel.Reset -= OnChapterPanelReset;
            On.Celeste.SaveData.FoundAnyCheckpoints -= OnSaveDataFoundAnyCheckpoints;
            On.Celeste.OuiChapterPanel.GetModeHeight -= OnChapterPanelGetModeHeight;
            On.Celeste.OuiChapterPanel.Swap -= OnChapterPanelSwap;
            On.Celeste.OuiChapterPanel.DrawCheckpoint -= OnChapterPanelDrawCheckpoint;
            On.Celeste.OuiJournal.Enter -= OnJournalEnter;
            On.Celeste.OuiChapterPanel.UpdateStats -= OnChapterPanelUpdateStats;
            IL.Celeste.OuiChapterPanel.Render -= ModOuiChapterPanelRender;
            IL.Celeste.DeathsCounter.Render -= ModDeathsCounterRender;
            IL.Celeste.StrawberriesCounter.Render -= ModStrawberriesCounterRender;
            On.Celeste.OuiChapterPanel.Start -= OnOuiChapterPanelStart;
            On.Celeste.Player.Die -= OnPlayerDie;

            onReloadLevelHook?.Dispose();
            onReloadLevelHook = null;

            IL.Celeste.OuiChapterPanel._FixTitleLength -= ModFixTitleLength;
            On.Celeste.OuiMainMenu.CreateButtons -= OnOuiMainMenuCreateButtons;

            foreach (Hook hook in altSidesHelperHooks) {
                hook.Dispose();
            }
            altSidesHelperHooks.Clear();

            hookOnMapDataOrigLoad?.Dispose();
            hookOnMapDataOrigLoad = null;

            onChangePresenceHook?.Dispose();
            onChangePresenceHook = null;
        }

        internal static AreaData collabInGameForcedArea;
        internal static bool saveAndReturnToLobbyAllowed;
        internal static string gymExitMapSID;
        internal static bool gymExitSaveAllowed;
        internal static ChapterPanelTrigger.ReturnToLobbyMode returnToLobbyMode;
        private static FancyText.Text panelCollabCredits;
        private static List<CreditsTag> panelCollabCreditsTags;
        private static bool exitFromGym;
        private static bool heartDirty;
        private static string[] activeGymTech;
        private static readonly HashSet<DeathsCounter> deathsCountersAddedByCollabUtils = new HashSet<DeathsCounter>();

        private static void OnOuiChapterPanelStart(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self, string checkpoint) {
            if (overworldWrapper != null) {
                (overworldWrapper.Scene as Level).PauseLock = true;

                if (gymSubmenuSelected(self)) {
                    // We picked a map in the second menu: this is a gym.
                    self.Area.Mode = AreaMode.Normal;
                    gymExitMapSID = collabInGameForcedArea.SID;
                    gymExitSaveAllowed = saveAndReturnToLobbyAllowed;
                    saveAndReturnToLobbyAllowed = false;
                } else if (returnToLobbySelected(self)) {
                    // The third option is "return to lobby".
                    self.Focused = false;
                    Audio.Play("event:/ui/world_map/chapter/back");
                    returnToLobbyMode = ChapterPanelTrigger.ReturnToLobbyMode.RemoveReturn;
                    self.Add(new Coroutine(ExitFromGymToLobbyRoutine(self)));
                    return;
                } else if (checkpoint != "collabutils_continue") {
                    // "continue" was not selected, so drop the saved state to start over.
                    CollabModule.Instance.SaveData.SessionsPerLevel.Remove(self.Area.SID);
                    CollabModule.Instance.SaveData.ModSessionsPerLevel.Remove(self.Area.SID);
                    CollabModule.Instance.SaveData.ModSessionsPerLevelBinary.Remove(self.Area.SID);
                }
            }

            orig(self, checkpoint);
        }

        private static IEnumerator ExitFromGymToLobbyRoutine(OuiChapterPanel self) {
            self.EnteringChapter = true;
            self.Add(new Coroutine(self.EaseOut(false)));

            yield return 0.2f;

            ScreenWipe.WipeColor = Color.Black;
            AreaData.Get(self.Area).Wipe(self.Overworld, false, null);
            Audio.SetMusic(null);
            Audio.SetAmbience(null);

            yield return 0.5f;

            foreach (LevelEndingHook component in (overworldWrapper.Scene as Level).Tracker.GetComponents<LevelEndingHook>()) {
                component.OnEnd?.Invoke();
            }

            Engine.Scene = new LevelExitToLobby(LevelExit.Mode.GiveUp, SaveData.Instance.CurrentSession_Safe);
        }

        private static void OnPause(Level level, int startIndex, bool minimal, bool quickReset) {
            if (overworldWrapper != null) {
                Close(level, true, true);
            }
        }

        private static PlayerDeadBody OnPlayerDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats) {
            PlayerDeadBody result = orig(self, direction, evenIfInvincible, registerDeathInStats);
            if (result != null && self.Scene is Level level && overworldWrapper != null) {
                // the player died, so we should close the "in-game overworld" before weird stuff happens.
                Close(level, true, true);
            }
            return result;
        }

        private static void OnReloadLevel(Action orig) {
            if (overworldWrapper != null) {
                if (!(Engine.Scene is Level level)) {
                    level = AssetReloadHelper.ReturnToScene as Level;
                }

                if (level != null) {
                    // the level is about to get reloaded: make sure to unlock player movement if it is locked.
                    // (no need to close the in-game overworld, the reload will do that for us.)
                    Player player = level.Tracker.GetEntity<Player>();
                    if (player != null && player.StateMachine.State == Player.StDummy) {
                        player.StateMachine.State = Player.StNormal;
                    }
                }
            }

            orig();
        }


        private static void OnDiscordChangePresence(Action<Everest.DiscordSDK, Session> orig, Everest.DiscordSDK self, Session session) {
            if (!presenceLock) {
                orig(self, session);
            }
        }

        private static void OnOuiMainMenuCreateButtons(On.Celeste.OuiMainMenu.orig_CreateButtons orig, OuiMainMenu self) {
            orig(self);
            presenceLock = false;
        }

        private static bool OnSetMusic(On.Celeste.Audio.orig_SetMusic orig, string path, bool startPlaying, bool allowFadeOut) {
            // while the in-game chapter panel / journal is open, block all music changes except for muting it (which happens when entering a level).
            if (path != null && overworldWrapper?.Scene == Engine.Scene) {
                return false;
            }

            return orig(path, startPlaying, allowFadeOut);
        }

        private static bool OnSetAmbience(On.Celeste.Audio.orig_SetAmbience orig, string path, bool startPlaying) {
            // while the in-game chapter panel / journal is open, block all ambience changes except for muting it (which happens when entering a level).
            if (path != null && overworldWrapper?.Scene == Engine.Scene) {
                return false;
            }

            return orig(path, startPlaying);
        }

        private static void OnChapterPanelReset(On.Celeste.OuiChapterPanel.orig_Reset orig, OuiChapterPanel self) {
            resetCrystalHeart(self);

            AreaData forceArea = self.Overworld == null ? null : collabInGameForcedArea;
            if (forceArea == null) {
                orig(self);
                customizeCrystalHeart(self);
                return;
            }

            SaveData save = SaveData.Instance;
            Session session = save.CurrentSession;
            lastArea = save.LastArea;

            save.LastArea = forceArea.ToKey();
            save.CurrentSession = null;

            OuiChapterSelect ouiChapterSelect = self.Overworld.GetUI<OuiChapterSelect>();
            OuiChapterSelectIcon icon = ouiChapterSelect.icons[save.LastArea.ID];
            icon.SnapToSelected();
            icon.Add(new Coroutine(UpdateIconRoutine(self, icon)));

            orig(self);
            customizeCrystalHeart(self);

            if (!isPanelShowingLobby()) {
                self.chapter = (collabInGameForcedArea.Name + "_author").DialogCleanOrNull() ?? "";

                if (CollabMapDataProcessor.GymLevels.ContainsKey(forceArea.SID)) {
                    CollabMapDataProcessor.GymLevelInfo info = CollabMapDataProcessor.GymLevels[forceArea.SID];

                    if (info.Tech.Any(name => CollabMapDataProcessor.GymTech.ContainsKey(name))) {
                        // some of the tech used here exists in gyms! be sure to display the "tech" tab.
                        self.modes.Add(new OuiChapterPanel.Option {
                            Label = Dialog.Clean("collabutils2_overworld_gym"),
                            BgColor = Calc.HexToColor("FFD07E"),
                            Bg = GFX.Gui[GetModdedPath(self, "areaselect/tab")],
                            Icon = GFX.Gui["CollabUtils2/menu/ppt"],
                        });
                    }
                }
            }

            // LastArea is also checked in Render.
            save.CurrentSession = session;

            if (exitFromGym) {
                self.modes.Add(new OuiChapterPanel.Option {
                    Label = Dialog.Clean("collabutils2_overworld_exit"),
                    BgColor = Calc.HexToColor("FA5139"),
                    Bg = GFX.Gui[GetModdedPath(self, "areaselect/tab")],
                    Icon = GFX.Gui["menu/exit"],
                });

                // directly select the current gym.
                ChapterPanelSwapToGym(self);
            }
        }

        private class OuiChapterPanelGymOption : OuiChapterPanel.Option {
            public string GymTechDifficuty;
        }
        
        private static MethodInfo m_OuiChapterPanel__ModAreaselectTexture = typeof(OuiChapterPanel).GetMethod("_ModAreaselectTexture", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static string GetModdedPath(OuiChapterPanel panel, string path)
            => (string) m_OuiChapterPanel__ModAreaselectTexture.Invoke(panel, [path]);

        private static void ChapterPanelSwapToGym(OuiChapterPanel self) {
            self.Area.Mode = (AreaMode) 1;
            self.Overworld.ShowInputUI = true;

            self.UpdateStats(false);

            self.resizing = false;
            self.selectingMode = false;
            self.contentOffset = new Vector2(440f, self.contentOffset.Y);
            self.height = 730f;
            self.option = 0;
            activeGymTech = CollabMapDataProcessor.GymLevels[collabInGameForcedArea.SID].Tech;

            List<OuiChapterPanel.Option> checkpoints = self.checkpoints;
            checkpoints.Clear();
            string[] tech = activeGymTech.Where(name => CollabMapDataProcessor.GymTech.ContainsKey(name)).ToArray();

            for (int i = 0; i < tech.Length; i++) {
                string techName = tech[i];

                CollabMapDataProcessor.GymTechInfo techInfo = CollabMapDataProcessor.GymTech[techName];
                var checkpoint = new OuiChapterPanelGymOption {
                    Label = Dialog.Clean($"{LobbyHelper.GetCollabNameForSID(techInfo.AreaSID)}_gym_{techName}_name", null),
                    BgColor = difficultyColors[techInfo.Difficulty],
                    Bg = GFX.Gui[GetModdedPath(self, "areaselect/tab")],
                    Icon = GFX.Gui[$"CollabUtils2/areaselect/startpoint_{techInfo.Difficulty}"],
                    CheckpointLevelName = $"{techInfo.AreaSID}|{techInfo.Level}",
                    Large = false,
                    Siblings = tech.Length,
                    GymTechDifficuty = techInfo.Difficulty
                };
                checkpoints.Add(checkpoint);

                string currentSid = SaveData.Instance.CurrentSession_Safe.Area.SID;
                string currentRoom = SaveData.Instance.CurrentSession_Safe.Level;

                if (techInfo.AreaSID == currentSid && techInfo.Level == currentRoom) {
                    // this is the one we're currently in! select it
                    self.option = i;
                }
            }

            for (int i = 0; i < checkpoints.Count; i++) {
                var option = checkpoints[i];
                option.Pop = self.option == i ? 1f : 0f;
                option.Appear = 1f;
                option.CheckpointSlideOut = self.option > i ? 1f : 0f;
                option.Faded = 0f;
                option.SlideTowards(i, checkpoints.Count, true);
            }

            List<OuiChapterPanel.Option> modes = self.modes;
            for (int i = 0; i < modes.Count; i++) {
                modes[i].SlideTowards(i, modes.Count, true);
            }

            self.Focused = true;
        }

        private static void resetCrystalHeart(OuiChapterPanel panel) {
            if (heartDirty) {
                panel.Remove(panel.heart);
                panel.heart = new HeartGemDisplay(0, false);
                panel.Add(panel.heart);
                heartDirty = false;
            }
        }

        private static void customizeCrystalHeart(OuiChapterPanel panel) {
            // customize heart gem icon
            string sid = panel.Area.SID;

            Sprite[] heartSprites = panel.heart.Sprites;
            for (int side = 0; side < 3; side++) {
                string animId = GetGuiHeartSpriteId(sid, (AreaMode) side);

                if (animId != null) {
                    Sprite heartSprite = HeartSpriteBank.Create(animId);
                    heartSprite.Visible = heartSprites[side].Visible;

                    heartSprites[side] = heartSprite;
                    heartSprite.Play("spin");
                    heartDirty = true;
                }
            }
        }

        private static string mapSideName(string mapSID, AreaMode side) {
            string sideName = mapSID.DialogKeyify();
            if (side == AreaMode.BSide) {
                sideName += "_B";
            } else if (side == AreaMode.CSide) {
                sideName += "_C";
            }

            return sideName;
        }

        /// <summary>
        /// Returns the GUI heart sprite ID (for display in the chapter panel) matching the given map and side, to read from the HeartSpriteBank.
        /// </summary>
        /// <param name="mapSID">The map SID to get the heart sprite for</param>
        /// <param name="side">The side to get the heart sprite for</param>
        /// <returns>The sprite ID to pass to HeartSpriteBank.Create to get the custom heart sprite, or null if none was found</returns>
        public static string GetGuiHeartSpriteId(string mapSID, AreaMode side) {
            string mapLevelSet = AreaData.Get(mapSID)?.LevelSet.DialogKeyify();
            string sideName = mapSideName(mapSID, side);

            if (OverrideHeartSpriteIDs.TryGetValue(sideName, out string spriteID) && HeartSpriteBank.Has(spriteID)) {
                // this map has an override custom heart registered: use it.
                return spriteID;
            } else if (HeartSpriteBank.Has("crystalHeart_" + sideName)) {
                // this map has a custom heart registered: use it.
                return "crystalHeart_" + sideName;
            } else if (HeartSpriteBank.Has("crystalHeart_" + mapLevelSet)) {
                // this level set has a custom heart registered: use it.
                return "crystalHeart_" + mapLevelSet;
            }

            return null;
        }

        /// <summary>
        /// Adds an override heart sprite ID to use for a given map.
        /// Useful when lots of heart sprites need to be overridden and replacing all of those manually in the sprite swap XML is too tedious.
        /// </summary>
        /// <param name="mapSID">The map SID to override the heart sprite for</param>
        /// <param name="side">The side to override the heart sprite for</param>
        /// <param name="spriteID">The sprite ID to override the map's heart with</param>
        public static void AddOverrideHeartSpriteID(string mapSID, AreaMode side, string spriteID) {
            string sideName = mapSideName(mapSID, side);

            if (OverrideHeartSpriteIDs.TryGetValue(sideName, out _))
                OverrideHeartSpriteIDs[sideName] = spriteID;
            else
                OverrideHeartSpriteIDs.Add(sideName, spriteID);
        }

        /// <summary>
        /// Removes the override heart sprite ID for a given map.
        /// </summary>
        /// <param name="mapSID">The map SID to remove the override for</param>
        /// <param name="side">The side to remove the override for</param>
        public static void RemoveOverrideHeartSpriteID(string mapSID, AreaMode side) {
            string sideName = mapSideName(mapSID, side);

            if (OverrideHeartSpriteIDs.TryGetValue(sideName, out _))
                OverrideHeartSpriteIDs.Remove(sideName);
        }

        // AltSidesHelper does very similar stuff to us, and we want to override what it does if the XMLs are asking for it.
        private static void resetCrystalHeartAfterAltSidesHelper(Action<OuiChapterPanel> orig, OuiChapterPanel panel) {
            orig(panel);
            resetCrystalHeart(panel);
        }

        private static void customizeCrystalHeartAfterAltSidesHelper(Action<OuiChapterPanel> orig, OuiChapterPanel panel) {
            orig(panel);
            customizeCrystalHeart(panel);
        }

        private static bool OnSaveDataFoundAnyCheckpoints(On.Celeste.SaveData.orig_FoundAnyCheckpoints orig, SaveData self, AreaKey area) {
            if (Engine.Scene == overworldWrapper?.Scene) {
                if (gymSubmenuSelected()) {
                    // the gym button always has a submenu to pick a tech.
                    return true;
                }
                if (returnToLobbySelected()) {
                    // the return to lobby button never has a submenu.
                    return false;
                }

                // for entering the map, display the second page (containing the credits) if they are defined in English.txt.
                // otherwise, if there is a saved state, also display the chapter panel.
                return orig(self, area) ||
                    Dialog.Has(collabInGameForcedArea.Name + "_collabcredits") ||
                    Dialog.Has(collabInGameForcedArea.Name + "_collabcreditstags") ||
                    CollabModule.Instance.SaveData.SessionsPerLevel.ContainsKey(area.SID);
            }

            return orig(self, area);
        }

        private static int OnChapterPanelGetModeHeight(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
            // force the chapter panel to be bigger if deaths > 0 (we force deaths to display even if the player didn't beat the map) or if there is a speed berry PB,
            // because in these cases we have stuff to display in the chapter panel, and vanilla wouldn't display anything.
            AreaModeStats areaModeStats = self.RealStats.Modes[(int) self.Area.Mode];
            if (Engine.Scene == overworldWrapper?.Scene && !AreaData.Get(self.Area).Interlude_Safe
                && (areaModeStats.Deaths > 0 || CollabModule.Instance.SaveData.SpeedBerryPBs.ContainsKey(self.Area.SID))) {

                return 540;
            }

            return orig(self);
        }

        private static bool ShouldModChapterPanelSwap(OuiChapterPanel self)
            => Engine.Scene == overworldWrapper?.Scene && (gymSubmenuSelected(self)
                || Dialog.Has(collabInGameForcedArea.Name + "_collabcredits")
                || Dialog.Has(collabInGameForcedArea.Name + "_collabcreditstags")
                || CollabModule.Instance.SaveData.SessionsPerLevel.ContainsKey(self.Area.SID));

        private static void OnChapterPanelSwap(On.Celeste.OuiChapterPanel.orig_Swap orig, OuiChapterPanel self) {
            if (!ShouldModChapterPanelSwap(self) || !self.selectingMode) {
                // this isn't an in-game chapter panel,
                // or there is no custom second page (no credits, no saved state, no gyms),
                // or we are not swapping from the mode select screen => use vanilla
                orig(self);
                return;
            }

            if (gymSubmenuSelected(self)) {
                activeGymTech = CollabMapDataProcessor.GymLevels[collabInGameForcedArea.SID].Tech;
            } else {
                string areaName = collabInGameForcedArea.Name;
                panelCollabCredits = FancyText.Parse(Dialog.Get(areaName + "_collabcredits").Replace("{break}", "{n}"), int.MaxValue, int.MaxValue, defaultColor: Color.Black);

                if (Dialog.Has(areaName + "_collabcreditstags")) {
                    panelCollabCreditsTags = CreditsTag.Parse(Dialog.Get(areaName + "_collabcreditstags"));
                } else {
                    panelCollabCreditsTags = new List<CreditsTag>();
                }
            }

            orig(self);
        }

        private static void ModOuiChapterPanelSwapRoutine(ILContext il) {
            ILCursor cursor = new(il);

            // mod the routine's `toHeight`
            if (cursor.TryGotoNextBestFit(MoveType.AfterLabel,
                instr => instr.MatchStfld(out FieldReference toHeight)
                    && toHeight.DeclaringType.Name.Contains("SwapRoutine")
                    && toHeight.Name.Contains("toHeight"))) {
                cursor.EmitLdloc1();
                cursor.EmitDelegate(ModToHeight);
            }
            
            // replace the chapter panel's checkpoints with a single dummy checkpoint
            if (cursor.TryGotoNextBestFit(MoveType.After,
                instr => instr.MatchCall<OuiChapterPanel>("_GetCheckpoints"))) {
                cursor.EmitLdloc1();
                cursor.EmitDelegate(ModCheckpoints);
            }

            // mod the chapter panel's options
            if (cursor.TryGotoNextBestFit(MoveType.After,
                instr => instr.MatchLdloc1(),
                instr => instr.MatchLdcI4(0),
                instr => instr.MatchCallvirt<OuiChapterPanel>("set_option"))) {
                cursor.MoveAfterLabels();
                cursor.EmitLdloc1();
                cursor.EmitDelegate(ModOptions);
            }
            
            return;
            
            static int ModToHeight(int orig, OuiChapterPanel panel) {
                return ShouldModChapterPanelSwap(panel) && panel.selectingMode
                    ? gymSubmenuSelected(panel)
                        ? GetChapterPanelGymToHeight(panel)
                        : GetChapterPanelToHeight(panel)
                    : orig;
            }

            static HashSet<string> ModCheckpoints(HashSet<string> orig, OuiChapterPanel panel) {
                return ShouldModChapterPanelSwap(panel) && !panel.selectingMode
                    ? ["CollabUtils2_dummyCheckpoint"]
                    : orig;
            }

            static void ModOptions(OuiChapterPanel panel) {
                if (!ShouldModChapterPanelSwap(panel) || panel.selectingMode)
                    return;
                
                if (gymSubmenuSelected(panel)) {
                    SetupChapterPanelGymOptions(panel);
                } else {
                    SetupChapterPanelOptions(panel);
                }
            }
        }

        private static int GetChapterPanelToHeight(OuiChapterPanel self) {
            string forcedArea = collabInGameForcedArea.Name;
            return Dialog.Has(forcedArea + "_collabcredits") ? 730 : (Dialog.Has(forcedArea + "_collabcreditstags") ? 450 : 300);
        }

        private static void SetupChapterPanelOptions(OuiChapterPanel self) {
            List<OuiChapterPanel.Option> checkpoints = self.checkpoints;
            Color? startOptionColor = checkpoints.FirstOrDefault(option => option.Icon.AtlasPath.EndsWith("startpoint"))?.BgColor;
            Color? checkpointOptionColor = checkpoints.FirstOrDefault(option => option.Icon.AtlasPath.EndsWith("checkpoint"))?.BgColor;
            checkpoints.Clear();

            bool hasContinueOption = CollabModule.Instance.SaveData.SessionsPerLevel.ContainsKey(self.Area.SID);

            checkpoints.Add(new OuiChapterPanel.Option {
                Label = Dialog.Clean(hasContinueOption ? "collabutils2_chapterpanel_start" : "overworld_start", null),
                BgColor = startOptionColor ?? Calc.HexToColor("eabe26"),
                Bg = GFX.Gui[GetModdedPath(self, "areaselect/tab")],
                Icon = GFX.Gui[GetModdedPath(self, "areaselect/startpoint")],
                CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                Large = false,
                Siblings = hasContinueOption ? 2 : 1
            });

            if (hasContinueOption) {
                checkpoints.Add(new OuiChapterPanel.Option {
                    Label = Dialog.Clean("collabutils2_chapterpanel_continue", null),
                    BgColor = checkpointOptionColor ?? Calc.HexToColor("3c6180"),
                    Bg = GFX.Gui[GetModdedPath(self, "areaselect/tab")],
                    Icon = GFX.Gui[GetModdedPath(self, "areaselect/checkpoint")],
                    CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                    CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                    Large = false,
                    Siblings = 2,
                    CheckpointLevelName = "collabutils_continue"
                });
            }

            self.option = hasContinueOption ? 1 : 0;
        }

        private static int GetChapterPanelGymToHeight(OuiChapterPanel self) {
            return 730;
        }

        private static void SetupChapterPanelGymOptions(OuiChapterPanel self) {
            List<OuiChapterPanel.Option> checkpoints = self.checkpoints;
            checkpoints.Clear();

            string[] tech = activeGymTech.Where(name => CollabMapDataProcessor.GymTech.ContainsKey(name)).ToArray();
            foreach (string techName in tech) {
                CollabMapDataProcessor.GymTechInfo techInfo = CollabMapDataProcessor.GymTech[techName];
                var checkpoint = new OuiChapterPanelGymOption {
                    Label = Dialog.Clean($"{LobbyHelper.GetCollabNameForSID(techInfo.AreaSID)}_gym_{techName}_name", null),
                    BgColor = difficultyColors[techInfo.Difficulty],
                    Bg = GFX.Gui[GetModdedPath(self, "areaselect/tab")],
                    Icon = GFX.Gui[$"CollabUtils2/areaselect/startpoint_{techInfo.Difficulty}"],
                    CheckpointLevelName = $"{techInfo.AreaSID}|{techInfo.Level}",
                    Large = false,
                    Siblings = tech.Count(),
                    GymTechDifficuty = techInfo.Difficulty
                };
                checkpoints.Add(checkpoint);
            }

            self.option = 0;
        }

        private static void OnChapterPanelDrawCheckpoint(On.Celeste.OuiChapterPanel.orig_DrawCheckpoint orig, OuiChapterPanel self, Vector2 center, object option, int checkpointIndex) {
            if (overworldWrapper != null) {
                if (gymSubmenuSelected(self)) {
                    string[] collabTech = activeGymTech;
                    if (collabTech != null && collabTech.Length != 0) {
                        OnChapterPanelDrawGymCheckpoint(self, center, (OuiChapterPanel.Option) option, checkpointIndex, collabTech);
                        return;
                    }
                } else {
                    FancyText.Text collabCredits = panelCollabCredits;
                    if (collabCredits != null) {
                        OnChapterPanelDrawCollabCreditsCheckpoint(self, center, checkpointIndex);
                        return;
                    }
                }
            }

            orig(self, center, option, checkpointIndex);
        }

        private static void OnChapterPanelDrawCollabCreditsCheckpoint(OuiChapterPanel self, Vector2 center, int checkpointIndex) {
            FancyText.Text collabCredits = panelCollabCredits;

            if (checkpointIndex > 0) {
                return;
            }

            bool hasCredits = Dialog.Has(collabInGameForcedArea.Name + "_collabcredits");

            // panel height is 730 pixels when completely open, or 450 if there are only tags.
            // Tags should fade out quicker than text, because they are near the bottom of the panel, and it looks bad more quickly when the panel closes.
            float alphaText = Calc.ClampedMap(self.height, 600, 730);
            float alphaTags = Calc.ClampedMap(self.height, hasCredits ? 700 : 540, hasCredits ? 730 : 450);

            float heightTakenByTags = 0f;

            // draw tags.
            List<CreditsTag> collabCreditsTags = panelCollabCreditsTags;
            if (collabCreditsTags != null) {
                // split tags in lines, fitting as many tags as possible on each line.
                List<List<CreditsTag>> lines = new List<List<CreditsTag>>();
                List<float> lineWidths = new List<float>();

                // this block is responsible for splitting tags in lines.
                {
                    List<CreditsTag> line = new List<CreditsTag>();
                    float lineWidth = 0f;
                    for (int i = 0; i < collabCreditsTags.Count; i++) {
                        float advanceX = ActiveFont.Measure(collabCreditsTags[i].Text).X * 0.5f + 30f; // 30 = margin between tags
                        if (lineWidth + advanceX > 800f) {
                            // we exceeded the limit. we need a line break!
                            lines.Add(line.ToList());
                            lineWidths.Add(lineWidth);

                            line.Clear();
                            lineWidth = 0f;
                        }

                        // add the tag to the current line.
                        line.Add(collabCreditsTags[i]);
                        lineWidth += advanceX;
                    }

                    // add the last line.
                    lines.Add(line.ToList());
                    lineWidths.Add(lineWidth);
                }

                // now, draw the tags bottom up.
                float y = center.Y;
                if (hasCredits) {
                    y += 230f;
                } else {
                    y -= 80f + 30f - (26f * (lines.Count - 1));
                }

                for (int i = lines.Count - 1; i >= 0; i--) {
                    // starting position is all the way left.
                    float x = center.X - (lineWidths[i] / 2f) + 15f;

                    foreach (CreditsTag tag in lines[i]) {
                        // black edge > BaseColor text background > TextColor tag text
                        float width = ActiveFont.Measure(tag.Text).X * 0.5f;

                        if (tag.BorderTexture != null) {
                            for (int tex_x = 0; tex_x < width + 20; tex_x += tag.BorderTexture.Width)
                                tag.BorderTexture.Draw(new Vector2(x - 10 + tex_x, y - 6), Vector2.Zero, (tag.BorderColor ?? Color.White) * alphaTags, Vector2.One, 0f,
                                                       new Rectangle(0, 0, (int) width + 20 - tex_x, 44));
                        } else {
                            Draw.Rect(x - 10, y - 6, width + 20, 44, (tag.BorderColor ?? Color.Black) * alphaTags);
                        }

                        if (tag.FillTexture != null) {
                            for (int tex_x = 0; tex_x < width + 12; tex_x += tag.FillTexture.Width)
                                tag.FillTexture.Draw(new Vector2(x - 6 + tex_x, y - 2), Vector2.Zero, (tag.FillColor ?? Color.White) * alphaTags, Vector2.One, 0f,
                                                     new Rectangle(0, 0, (int) width + 12 - tex_x, 36));
                        } else {
                            Draw.Rect(x - 6, y - 2, width + 12, 36, (tag.FillColor ?? self.Data.TitleBaseColor) * alphaTags);
                        }

                        ActiveFont.Draw(tag.Text, new Vector2(x, y), Vector2.Zero, Vector2.One * 0.5f, (tag.TextColor ?? self.Data.TitleTextColor) * alphaTags);

                        // advance the position to the next tag.
                        x += width + 30f;
                    }

                    // move up 1 line.
                    y -= 52f;
                    heightTakenByTags += 52f;
                }
            }

            // compute the maximum scale the credits can take (max 1) to fit the remaining space.
            float creditsWidth = collabCredits.WidestLine();
            float creditsHeight = collabCredits.Font.Get(collabCredits.BaseSize).LineHeight * (collabCredits.Nodes.OfType<FancyText.NewLine>().Count() + 1);
            float scale = Math.Min(1f, Math.Min((410f - heightTakenByTags) / creditsHeight, 800f / creditsWidth));

            // draw the credits.
            collabCredits.DrawJustifyPerLine(
                center + new Vector2(0f, 40f - heightTakenByTags / 2f),
                Vector2.One * 0.5f,
                Vector2.One * scale,
                0.8f * alphaText);
        }

        private static void OnChapterPanelDrawGymCheckpoint(OuiChapterPanel self, Vector2 center, OuiChapterPanel.Option option, int checkpointIndex, string[] collabTech) {
            AreaData forcedArea = collabInGameForcedArea;

            if (CollabMapDataProcessor.GymTech.ContainsKey(collabTech[checkpointIndex])) {
                CollabMapDataProcessor.GymTechInfo techInfo = CollabMapDataProcessor.GymTech[collabTech[checkpointIndex]];

                string imageName = $"{LobbyHelper.GetCollabNameForSID(forcedArea.SID)}/Gyms/{collabTech[checkpointIndex]}";
                MTexture imagePreview = MTN.Checkpoints.Has(imageName) ? MTN.Checkpoints[imageName] : null;
                if (imagePreview != null) {
                    Vector2 vector = center + (Vector2.UnitX * 800f * Ease.CubeIn(option.CheckpointSlideOut));
                    imagePreview.DrawCentered(vector, Color.White, Vector2.One * 0.5f);
                }
            }
        }

        private static void ModOuiChapterPanelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // 1. Swap the "chapter xx" and the map name positions.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(-2f) || instr.MatchLdcR4(-18f))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel title position at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.EmitDelegate<Func<float, float>>(swapChapterNumberAndName);
            }

            cursor.Index = 0;

            // 2. Turn the chapter card silver or rainbow instead of gold when relevant.
            while (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdstr("areaselect/cardtop_golden") || instr.MatchLdstr("areaselect/card_golden"),
                instr => instr.MatchCall<OuiChapterPanel>("_ModCardTexture") || instr.MatchCall<OuiChapterPanel>("_ModAreaselectTexture"))) {

                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel card at {cursor.Index} in IL for OuiChapterPanel.Render");

                cursor.EmitDelegate<Func<string, string>>(reskinGoldenChapterCard);
            }

            cursor.Index = 0;

            // 3. If the author name is empty, center the map name like interludes.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<AreaData>("get_Interlude_Safe"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel title position at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<bool, OuiChapterPanel, bool>>(hideChapterNumberIfNecessary);
            }

            cursor.Index = 0;

            // 4. Keep forced area name even when it changes (for gyms)

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<AreaData>("Name"))) {
                Logger.Log("FlushelineCollab/InGameOverworldHelper", $"Modding chapter panel title name at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<string, OuiChapterPanel, string>>(modChapterPanelName);
            }

            cursor.Index = 0;

            // 5-1. Get line to jump to after the next injection.
            ILLabel afterOptionLabel = cursor.DefineLabel();

            if (cursor.TryGotoNext(MoveType.Before,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdfld<OuiChapterPanel>("selectingMode"))) {
                cursor.MarkLabel(afterOptionLabel);
            }

            cursor.Index = 0;

            // 5-2. Draw the difficulty underneath the checkpoint label in gyms.
            while (cursor.TryGotoNextBestFit(MoveType.Before,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchCallvirt<OuiChapterPanel>("get_options"),
                instr => instr.MatchLdarg(0),
                instr => instr.MatchCallvirt<OuiChapterPanel>("get_option"),
                instr => true,
                instr => instr.MatchLdfld(typeof(OuiChapterPanel.Option), "Label"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter option label position at {cursor.Index} in IL for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<OuiChapterPanel, bool>>(modChapterOptionLabelPosition);

                cursor.Emit(OpCodes.Brtrue, afterOptionLabel);
                cursor.Index++;
            }
        }

        private static string modChapterPanelName(string name, OuiChapterPanel self) {
            if (overworldWrapper != null) {
                AreaData forcedArea = collabInGameForcedArea;
                if (forcedArea != null) {
                    return forcedArea.Name;
                }
            }
            return name;
        }

        private static float swapChapterNumberAndName(float orig) {
            if (Engine.Scene == overworldWrapper?.Scene && !isPanelShowingLobby()) {
                return orig == -18f ? -49f : 43f;
            } else {
                return orig;
            }
        }

        private static string reskinGoldenChapterCard(string orig) {
            if (orig != "areaselect/cardtop_golden" && orig != "areaselect/card_golden") {
                // chapter card was reskinned through Everest, so don't change it.
                return orig;
            }

            string sid = getCurrentPanelMapSID();

            if (CollabMapDataProcessor.MapsWithRainbowBerries.Contains(sid)) {
                return orig == "areaselect/cardtop_golden" ? "CollabUtils2/chapterCard/cardtop_rainbow" : "CollabUtils2/chapterCard/card_rainbow";
            }
            if (CollabMapDataProcessor.MapsWithSilverBerries.Contains(sid)) {
                return orig == "areaselect/cardtop_golden" ? "CollabUtils2/chapterCard/cardtop_silver" : "CollabUtils2/chapterCard/card_silver";
            }
            return orig;
        }

        private static bool hideChapterNumberIfNecessary(bool orig, OuiChapterPanel self) {
            if (Engine.Scene == overworldWrapper?.Scene && self.chapter.Length == 0) {
                return true; // interlude!
            } else {
                return orig;
            }
        }

        private static bool modChapterOptionLabelPosition(OuiChapterPanel self) {
            if (overworldWrapper != null) {
                if (gymSubmenuSelected(self) && !self.selectingMode) {
                    var option = self.options[self.option];
                    string difficulty = option is OuiChapterPanelGymOption o ? o.GymTechDifficuty : null;
                    if (difficulty != null) {
                        string difficultyLabel = Dialog.Clean($"collabutils2_difficulty_{difficulty}");
                        Vector2 renderPos = self.OptionsRenderPosition;
                        ActiveFont.Draw(option.Label, renderPos + new Vector2(0f, -140f), new Vector2(0.5f, 1f), Vector2.One * (1f + self.wiggler.Value * 0.1f), Color.Black * 0.8f);
                        ActiveFont.Draw(difficultyLabel, renderPos + new Vector2(0f, -140f), new Vector2(0.5f, 0f), Vector2.One * 0.6f * (1f + self.wiggler.Value * 0.1f), Color.Black * 0.8f);
                        return true;
                    }
                }
            }
            return false;
        }

        private static IEnumerator OnJournalEnter(On.Celeste.OuiJournal.orig_Enter orig, OuiJournal self, Oui from) {
            IEnumerator origc = orig(self, from);

            SaveData save = SaveData.Instance;
            AreaData forceArea = collabInGameForcedArea;
            if (forceArea != null) {
                lastArea = save.LastArea;
                save.LastArea = forceArea.ToKey();
            }

            while (origc.MoveNext())
                yield return origc.Current;

            if (forceArea != null && lastArea != null) {
                save.LastArea = lastArea.Value;
                lastArea = null;
            }
        }


        private static void OnChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle,
            bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {

            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            DeathsCounter deathsCounter = self.deaths;

            if (Engine.Scene == overworldWrapper?.Scene) {
                // within lobbies, death counts always show up, even if you didn't beat the map yet.
                AreaModeStats areaModeStats = self.DisplayedStats.Modes[(int) self.Area.Mode];
                deathsCounter.Visible = areaModeStats.Deaths > 0 && !AreaData.Get(self.Area).Interlude_Safe;
            }

            // mod the death icon: for the path, use the current level set, or for lobbies, the lobby's matching level set.
            string pathToSkull = "CollabUtils2/skulls/" + self.Area.GetLevelSet();
            string lobbyLevelSet = LobbyHelper.GetLobbyLevelSet(self.Area.SID);
            if (lobbyLevelSet != null) {
                pathToSkull = "CollabUtils2/skulls/" + lobbyLevelSet;
            }
            if (GFX.Gui.Has(pathToSkull)) {
                deathsCounter.icon = GFX.Gui[pathToSkull];
                deathsCountersAddedByCollabUtils.Add(deathsCounter);
            }

            if (isPanelShowingLobby(self) || Engine.Scene == overworldWrapper?.Scene) {
                // turn strawberry counter into golden if there only are golden berries in the map
                MapData mapData = AreaData.Get(self.Area).Mode[0].MapData;
                if (mapData.DetectedStrawberriesIncludingUntracked == mapData.Goldenberries.Count) {
                    StrawberriesCounter strawberriesCounter = self.strawberries;
                    strawberriesCounter.Golden = true;
                    strawberriesCounter.ShowOutOf = false;
                }
            }
        }

        private static void ModDeathsCounterRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(62f))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Unhardcoding death icon width at {cursor.Index} in IL for DeathsCounter.Render");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(DeathsCounter).GetField("icon", BindingFlags.NonPublic | BindingFlags.Instance));
                cursor.EmitDelegate<Func<float, DeathsCounter, MTexture, float>>(unhardcodeDeathsCounterWidth);
            }
        }

        private static float unhardcodeDeathsCounterWidth(float orig, DeathsCounter self, MTexture icon) {
            if (deathsCountersAddedByCollabUtils.Contains(self)) {
                return icon.Width - 4; // vanilla icons are 66px wide.
            }
            return orig;
        }

        private static void ModStrawberriesCounterRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("collectables/goldberry"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Changing strawberry icon w/ silver one at {cursor.Index} in IL for StrawberriesCounter.Render");
                cursor.EmitDelegate<Func<string, string>>(modStrawberryIconInChapterPanel);
            }
        }

        private static string modStrawberryIconInChapterPanel(string orig) {
            string sid = getCurrentPanelMapSID();
            if (CollabMapDataProcessor.MapsWithRainbowBerries.Contains(sid)) {
                return "CollabUtils2/rainbowberry";
            }
            if (CollabMapDataProcessor.MapsWithSilverBerries.Contains(sid)) {
                return "CollabUtils2/silverberry";
            }
            return orig;
        }

        private static void ModMapDataLoad(Action<MapData> orig, MapData self) {
            orig(self);

            // add the silver/rainbow berries as golden berries in map data. This is what will make the chapter card golden.
            foreach (LevelData level in self.Levels) {
                foreach (EntityData entity in level.Entities) {
                    if (entity.Name == "CollabUtils2/SilverBerry" || entity.Name == "CollabUtils2/RainbowBerry") {
                        self.Goldenberries.Add(entity);
                    }
                }
            }
        }

        private static IEnumerator UpdateIconRoutine(OuiChapterPanel panel, OuiChapterSelectIcon icon) {
            Overworld overworld = overworldWrapper?.WrappedScene;
            if (overworld == null)
                yield break;

            while (overworld.Current == panel || overworld.Last == panel || overworld.Next == panel) {
                icon.Position = panel.Position + panel.IconOffset;
                yield return null;
            }
        }

        public static void OpenChapterPanel(Player player, string sid, ChapterPanelTrigger.ReturnToLobbyMode returnToLobbyMode, bool savingAllowed, bool exitFromGym) {
            AreaData areaData = (AreaData.Get(sid) ?? AreaData.Get(0));
            if (!Dialog.Has(areaData.Name + "_collabcredits") && areaData.Mode[0].Checkpoints?.Length > 0) {
                // saving isn't compatible with checkpoints, because both would appear on the same page.
                savingAllowed = false;
            }

            player.Drop();
            presenceLock = true;

            Open(player, AreaData.Get(sid) ?? AreaData.Get(0), out OuiHelper_EnterChapterPanel.Start,
                overworld => {
                    InGameOverworldHelper.returnToLobbyMode = returnToLobbyMode;
                    saveAndReturnToLobbyAllowed = savingAllowed;
                    InGameOverworldHelper.exitFromGym = exitFromGym;
                });
        }

        public static void OpenJournal(Player player, string levelset) {
            player.Drop();
            presenceLock = true;

            Open(player, AreaData.Areas.FirstOrDefault(area => area.LevelSet == levelset) ?? AreaData.Get(0), out OuiHelper_EnterJournal.Start);
        }

        public static void Open(Player player, AreaData area, out bool opened, Action<Overworld> callback = null) {
            opened = false;

            if (overworldWrapper?.Scene == Engine.Scene || player.StateMachine.State == Player.StDummy)
                return;
            player.StateMachine.State = Player.StDummy;

            opened = true;

            Level level = player.Scene as Level;
            level.Entities.FindFirst<TotalStrawberriesDisplay>().Active = false;

            overworldWrapper = new SceneWrappingEntity<Overworld>(new Overworld(new OverworldLoader((Overworld.StartMode) (-1),
                new HiresSnow() {
                    Alpha = 0f,
                    ParticleAlpha = 0.25f,
                }
            )));
            overworldWrapper.OnBegin += (overworld) => {
                overworld.RendererList.Remove(overworld.RendererList.Renderers.Find(r => r is MountainRenderer));
                overworld.RendererList.Remove(overworld.RendererList.Renderers.Find(r => r is ScreenWipe));
                overworld.RendererList.UpdateLists();
            };
            overworldWrapper.OnEnd += (overworld) => {
                if (overworldWrapper?.WrappedScene == overworld) {
                    overworldWrapper = null;

                    // forget everything!
                    collabInGameForcedArea = null;
                    saveAndReturnToLobbyAllowed = false;
                    gymExitMapSID = null;
                    gymExitSaveAllowed = false;
                    returnToLobbyMode = default;
                    panelCollabCredits = null;
                    panelCollabCreditsTags = null;
                    exitFromGym = false;
                    heartDirty = false;
                    activeGymTech = null;
                    deathsCountersAddedByCollabUtils.Clear();
                }
            };

            level.Add(overworldWrapper);
            collabInGameForcedArea = area;
            callback?.Invoke(overworldWrapper.WrappedScene);

            overworldWrapper.Add(new Coroutine(UpdateRoutine(overworldWrapper)));
        }

        public static void Close(Level level, bool removeScene, bool resetPlayer) {
            if (removeScene) {
                overworldWrapper?.WrappedScene?.GetUI<OuiChapterPanel>().RemoveSelf();
                overworldWrapper?.WrappedScene?.Entities.UpdateLists();
                overworldWrapper?.RemoveSelf();

                if (lastArea != null && SaveData.Instance != null) {
                    SaveData.Instance.LastArea = lastArea.Value;
                    lastArea = null;
                    level.Entities.FindFirst<TotalStrawberriesDisplay>().Active = true;
                }
            }

            if (resetPlayer) {
                Player player = level.Tracker.GetEntity<Player>();
                if (player != null && player.StateMachine.State == Player.StDummy) {
                    Engine.Scene.OnEndOfFrame += () => {
                        player.StateMachine.State = Player.StNormal;
                    };
                }
            }
        }

        private static IEnumerator DelayedCloseRoutine(Level level) {
            yield return null;
            Close(level, false, true);
        }

        private static IEnumerator UpdateRoutine(SceneWrappingEntity<Overworld> wrapper) {
            Level level = wrapper.Scene as Level;
            Overworld overworld = wrapper.WrappedScene;

            while (overworldWrapper?.Scene == Engine.Scene) {
                if (overworld.Next is OuiChapterSelect) {
                    overworld.Next.RemoveSelf();
                    overworldWrapper.Add(new Coroutine(DelayedCloseRoutine(level)));
                }

                overworld.Snow.ParticleAlpha = 0.25f;

                if (overworld.Current != null || overworld.Next?.Scene != null) {
                    overworld.Snow.Alpha = Calc.Approach(overworld.Snow.Alpha, 1f, Engine.DeltaTime * 2f);

                } else {
                    overworld.Snow.Alpha = Calc.Approach(overworld.Snow.Alpha, 0, Engine.DeltaTime * 2f);
                    if (overworld.Snow.Alpha <= 0.01f) {
                        Close(level, true, true);
                    }
                }

                yield return null;
            }

            if (wrapper.Scene != null) {
                wrapper.RemoveSelf();
            }
        }

        private static string getCurrentPanelMapSID() {
            OuiChapterPanel panel = overworldWrapper?.WrappedScene?.GetUI<OuiChapterPanel>();
            if (panel == null) {
                panel = (Engine.Scene as Overworld)?.GetUI<OuiChapterPanel>();
            }
            if (panel == null) {
                panel = (AssetReloadHelper.ReturnToScene as Overworld).GetUI<OuiChapterPanel>();
            }
            string sid = panel.Area.SID;
            return sid;
        }

        private static OuiChapterPanel getChapterPanel() {
            OuiChapterPanel panel = null;
            if (overworldWrapper != null) {
                panel = overworldWrapper.WrappedScene?.GetUI<OuiChapterPanel>();
            }
            if (panel == null) {
                panel = (Engine.Scene as Overworld)?.GetUI<OuiChapterPanel>();
            }
            if (panel == null) {
                panel = (AssetReloadHelper.ReturnToScene as Overworld).GetUI<OuiChapterPanel>();
            }
            return panel;
        }

        private static bool isPanelShowingLobby(OuiChapterPanel panel = null) {
            panel = panel ?? getChapterPanel();
            return LobbyHelper.IsCollabLobby(panel?.Area.SID ?? "");
        }

        private static bool gymSubmenuSelected(OuiChapterPanel panel = null) {
            panel = panel ?? getChapterPanel();
            return panel != null && (gymExitMapSID != null ||
                (panel.Area.Mode == AreaMode.BSide && CollabMapDataProcessor.GymLevels.ContainsKey(collabInGameForcedArea.SID)));
        }

        private static bool returnToLobbySelected(OuiChapterPanel panel = null) {
            panel = panel ?? getChapterPanel();
            return panel != null && panel.Area.Mode == AreaMode.CSide && exitFromGym;
        }

        private static void ModFixTitleLength(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchCallOrCallvirt(typeof(ActiveFont), "Measure"),
                instr => instr.MatchLdfld<Vector2>("X"),
                instr => instr.MatchStloc(0))
            ) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel title bookmark position at {cursor.Index} in IL for OuiChapterPanel._FixTitleLength");
                cursor.Index--;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, OuiChapterPanel, float>>(resizeChapterPanelTitleTag);
            }
        }

        private static float resizeChapterPanelTitleTag(float orig, OuiChapterPanel self) {
            if (Engine.Scene == overworldWrapper?.Scene) {
                string mapAuthor = self.chapter;
                if (mapAuthor?.Length != 0) {
                    // if the map has author, use the wider one between it and the map title
                    float width = ActiveFont.Measure(mapAuthor).X * 0.6f;
                    return Math.Max(orig, width);
                }
            }

            return orig;
        }

        // ModInterop exports

        [ModExportName("CollabUtils2.InGameOverworldHelper")]
        private static class ModExports {
            public static SpriteBank GetHeartSpriteBank() {
                return HeartSpriteBank;
            }
            public static void AddOverrideHeartSpriteID(string mapSID, AreaMode side, string spriteID) {
                InGameOverworldHelper.AddOverrideHeartSpriteID(mapSID, side, spriteID);
            }
            public static void RemoveOverrideHeartSpriteID(string mapSID, AreaMode side, string spriteID) {
                InGameOverworldHelper.RemoveOverrideHeartSpriteID(mapSID, side);
            }
            public static string GetGuiHeartSpriteId(string mapSID, AreaMode side) {
                return InGameOverworldHelper.GetGuiHeartSpriteId(mapSID, side);
            }
        }
    }
}
