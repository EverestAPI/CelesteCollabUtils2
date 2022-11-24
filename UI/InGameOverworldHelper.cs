using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
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

namespace Celeste.Mod.CollabUtils2.UI {
    public static class InGameOverworldHelper {
        public static bool IsOpen => overworldWrapper?.Scene == Engine.Scene;

        private static SceneWrappingEntity<Overworld> overworldWrapper;

        public static SpriteBank HeartSpriteBank;

        private static AreaKey? lastArea;

        private static readonly Type t_OuiChapterPanelOption = typeof(OuiChapterPanel)
            .GetNestedType("Option", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        private static MethodInfo m_PlayExpandSfx = typeof(OuiChapterPanel)
            .GetMethod("PlayExpandSfx", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo m_UpdateStats = typeof(OuiChapterPanel)
            .GetMethod("UpdateStats", BindingFlags.NonPublic | BindingFlags.Instance);

        private static List<Hook> altSidesHelperHooks = new List<Hook>();
        private static Hook hookOnMapDataOrigLoad;

        private static Dictionary<string, Color> difficultyColors = new Dictionary<string, Color>() {
            { "beginner", Calc.HexToColor("56B3FF") },
            { "intermediate", Calc.HexToColor("FF6D81") },
            { "advanced", Calc.HexToColor("FFFF89") },
            { "expert", Calc.HexToColor("FF9E66") },
            { "grandmaster", Calc.HexToColor("DD87FF") }
        };

        private static Hook hookOnDiscordRichPresenceChange;

        private static bool presenceLock = false;

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
            On.Celeste.Mod.AssetReloadHelper.ReloadLevel += OnReloadLevel;
            IL.Celeste.OuiChapterPanel._FixTitleLength += ModFixTitleLength;
            On.Celeste.OuiMainMenu.CreateButtons += OnOuiMainMenuCreateButtons;

            // hooks the Discord rich presence update method of stable version 3650
            MethodInfo discordRichPresence = typeof(EverestModule).Assembly.GetType("Celeste.Mod.Everest+Discord")?.GetMethod("UpdateText");
            if (discordRichPresence != null) {
                hookOnDiscordRichPresenceChange = new Hook(discordRichPresence, typeof(InGameOverworldHelper)
                    .GetMethod("OnDiscordChangePresenceOld", BindingFlags.NonPublic | BindingFlags.Static));
            }

            // hooks the Discord rich presence update method of pull request https://github.com/EverestAPI/Everest/pull/543
            discordRichPresence = typeof(EverestModule).Assembly.GetType("Celeste.Mod.Everest+DiscordSDK")?.GetMethod("UpdatePresence", BindingFlags.NonPublic | BindingFlags.Instance);
            if (discordRichPresence != null) {
                hookOnDiscordRichPresenceChange = new Hook(discordRichPresence, typeof(InGameOverworldHelper)
                    .GetMethod("OnDiscordChangePresenceNew", BindingFlags.NonPublic | BindingFlags.Static));
            }

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
            On.Celeste.Mod.AssetReloadHelper.ReloadLevel -= OnReloadLevel;
            IL.Celeste.OuiChapterPanel._FixTitleLength -= ModFixTitleLength;
            On.Celeste.OuiMainMenu.CreateButtons -= OnOuiMainMenuCreateButtons;

            foreach (Hook hook in altSidesHelperHooks) {
                hook.Dispose();
            }
            altSidesHelperHooks.Clear();

            hookOnMapDataOrigLoad?.Dispose();
            hookOnMapDataOrigLoad = null;

            hookOnDiscordRichPresenceChange?.Dispose();
            hookOnDiscordRichPresenceChange = null;
        }

        private static void OnOuiChapterPanelStart(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self, string checkpoint) {
            if (overworldWrapper != null) {
                (overworldWrapper.Scene as Level).PauseLock = true;

                DynData<Overworld> overworldData = new DynData<Overworld>(self.Overworld);

                if (gymSubmenuSelected(self)) {
                    // We picked a map in the second menu: this is a gym.
                    self.Area.Mode = AreaMode.Normal;
                    overworldData["gymExitMapSID"] = overworldData.Get<AreaData>("collabInGameForcedArea").GetSID();
                    overworldData["gymExitSaveAllowed"] = overworldData.Get<bool>("saveAndReturnToLobbyAllowed");
                    overworldData["saveAndReturnToLobbyAllowed"] = false;
                } else if (returnToLobbySelected(self)) {
                    // The third option is "return to lobby".
                    self.Focused = false;
                    Audio.Play("event:/ui/world_map/chapter/back");
                    overworldData["returnToLobbyMode"] = ChapterPanelTrigger.ReturnToLobbyMode.RemoveReturn;
                    self.Add(new Coroutine(ExitFromGymToLobbyRoutine(self)));
                    return;
                } else if (checkpoint != "collabutils_continue") {
                    // "continue" was not selected, so drop the saved state to start over.
                    CollabModule.Instance.SaveData.SessionsPerLevel.Remove(self.Area.GetSID());
                    CollabModule.Instance.SaveData.ModSessionsPerLevel.Remove(self.Area.GetSID());
                    CollabModule.Instance.SaveData.ModSessionsPerLevelBinary.Remove(self.Area.GetSID());
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

        private static void OnReloadLevel(On.Celeste.Mod.AssetReloadHelper.orig_ReloadLevel orig) {
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

        private static void OnDiscordChangePresenceOld(Action<string, string, Session> orig, string details, string state, Session session) {
            if (!presenceLock) {
                orig(details, state, session);
            }
        }


        private static void OnDiscordChangePresenceNew(Action<object, Session> orig, object self, Session session) {
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

            AreaData forceArea = self.Overworld == null ? null : new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea");
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

            DynData<OuiChapterSelect> ouiChapterSelect = new DynData<OuiChapterSelect>(self.Overworld.GetUI<OuiChapterSelect>());
            OuiChapterSelectIcon icon = ouiChapterSelect.Get<List<OuiChapterSelectIcon>>("icons")[save.LastArea.ID];
            icon.SnapToSelected();
            icon.Add(new Coroutine(UpdateIconRoutine(self, icon)));

            orig(self);
            customizeCrystalHeart(self);

            DynData<OuiChapterPanel> data = new DynData<OuiChapterPanel>(self);
            data["hasCollabCredits"] = true;

            if (!isPanelShowingLobby()) {
                data["chapter"] = (new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea").Name + "_author").DialogCleanOrNull() ?? "";

                if (CollabMapDataProcessor.GymLevels.ContainsKey(forceArea.GetSID())) {
                    CollabMapDataProcessor.GymLevelInfo info = CollabMapDataProcessor.GymLevels[forceArea.GetSID()];

                    if (info.Tech.Any(name => CollabMapDataProcessor.GymTech.ContainsKey(name))) {
                        // some of the tech used here exists in gyms! be sure to display the "tech" tab.
                        data.Get<IList>("modes").Add(DynamicData.New(t_OuiChapterPanelOption)(new {
                            Label = Dialog.Clean("collabutils2_overworld_gym"),
                            BgColor = Calc.HexToColor("FFD07E"),
                            Icon = GFX.Gui["CollabUtils2/menu/ppt"],
                        }));
                    }
                }
            }

            // LastArea is also checked in Render.
            save.CurrentSession = session;

            if (new DynData<Overworld>(self.Overworld).Data.TryGetValue("exitFromGym", out object val) && (bool) val) {
                data.Get<IList>("modes").Add(DynamicData.New(t_OuiChapterPanelOption)(new {
                    Label = Dialog.Clean("collabutils2_overworld_exit"),
                    BgColor = Calc.HexToColor("FA5139"),
                    Icon = GFX.Gui["menu/exit"],
                }));

                // directly select the current gym.
                ChapterPanelSwapToGym(self, data);
            }
        }

        private static void ChapterPanelSwapToGym(OuiChapterPanel self, DynData<OuiChapterPanel> data) {
            self.Area.Mode = (AreaMode) 1;
            self.Overworld.ShowInputUI = true;

            m_UpdateStats?.Invoke(self, new object[] { false, null, null, null });

            data["resizing"] = false;
            data["selectingMode"] = false;
            data["contentOffset"] = new Vector2(440f, data.Get<Vector2>("contentOffset").Y);
            data["height"] = 730f;
            data["option"] = 0;
            data["gymTech"] = CollabMapDataProcessor.GymLevels[new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea").GetSID()].Tech;

            IList checkpoints = data.Get<IList>("checkpoints");
            checkpoints.Clear();
            string[] tech = data.Get<string[]>("gymTech").Where(name => CollabMapDataProcessor.GymTech.ContainsKey(name)).ToArray();

            for (int i = 0; i < tech.Length; i++) {
                string techName = tech[i];

                CollabMapDataProcessor.GymTechInfo techInfo = CollabMapDataProcessor.GymTech[techName];
                var checkpoint = DynamicData.New(t_OuiChapterPanelOption)(new {
                    Label = Dialog.Clean($"{LobbyHelper.GetCollabNameForSID(techInfo.AreaSID)}_gym_{techName}_name", null),
                    BgColor = difficultyColors[techInfo.Difficulty],
                    Icon = GFX.Gui[$"CollabUtils2/areaselect/startpoint_{techInfo.Difficulty}"],
                    CheckpointLevelName = $"{techInfo.AreaSID}|{techInfo.Level}",
                    Large = false,
                    Siblings = tech.Length
                });

                new DynamicData(checkpoint).Set("gymTechDifficulty", techInfo.Difficulty);
                checkpoints.Add(checkpoint);

                string currentSid = SaveData.Instance.CurrentSession_Safe.Area.GetSID();
                string currentRoom = SaveData.Instance.CurrentSession_Safe.Level;

                if (techInfo.AreaSID == currentSid && techInfo.Level == currentRoom) {
                    // this is the one we're currently in! select it
                    data["option"] = i;
                }
            }

            for (int i = 0; i < checkpoints.Count; i++) {
                var option = new DynamicData(checkpoints[i]);
                option.Set("Pop", data.Get<int>("option") == i ? 1f : 0f);
                option.Set("Appear", 1f);
                option.Set("CheckpointSlideOut", data.Get<int>("option") > i ? 1f : 0f);
                option.Set("Faded", 0f);
                option.Invoke("SlideTowards", i, checkpoints.Count, true);
            }

            IList modes = data.Get<IList>("modes");
            for (int i = 0; i < modes.Count; i++) {
                new DynamicData(modes[i]).Invoke("SlideTowards", i, modes.Count, true);
            }

            self.Focused = true;
        }

        private static void resetCrystalHeart(OuiChapterPanel panel) {
            DynData<OuiChapterPanel> panelData = new DynData<OuiChapterPanel>(panel);
            if (panelData.Data.ContainsKey("heartDirty") && panelData.Get<bool>("heartDirty")) {
                panel.Remove(panelData["heart"] as HeartGemDisplay);
                panelData["heart"] = new HeartGemDisplay(0, false);
                panel.Add(panelData["heart"] as HeartGemDisplay);
                panelData["heartDirty"] = false;
            }
        }

        private static void customizeCrystalHeart(OuiChapterPanel panel) {
            // customize heart gem icon
            string sid = panel.Area.GetSID();

            Sprite[] heartSprites = new DynData<OuiChapterPanel>(panel).Get<HeartGemDisplay>("heart").Sprites;
            for (int side = 0; side < 3; side++) {
                string animId = GetGuiHeartSpriteId(sid, (AreaMode) side);

                if (animId != null) {
                    Sprite heartSprite = HeartSpriteBank.Create(animId);
                    heartSprite.Visible = heartSprites[side].Visible;

                    heartSprites[side] = heartSprite;
                    heartSprite.Play("spin");
                    new DynData<OuiChapterPanel>(panel)["heartDirty"] = true;
                }
            }
        }

        /// <summary>
        /// Returns the GUI heart sprite ID (for display in the chapter panel) matching the given map and side, to read from the HeartSpriteBank.
        /// </summary>
        /// <param name="mapSID">The map SID to get the heart sprite for</param>
        /// <param name="side">The side to get the heart sprite for</param>
        /// <returns>The sprite ID to pass to HeartSpriteBank.Create to get the custom heart sprite, or null if none was found</returns>
        public static string GetGuiHeartSpriteId(string mapSID, AreaMode side) {
            string mapLevelSet = AreaData.Get(mapSID)?.GetLevelSet().DialogKeyify();

            string sideName = mapSID.DialogKeyify();
            if (side == AreaMode.BSide) {
                sideName += "_B";
            } else if (side == AreaMode.CSide) {
                sideName += "_C";
            }

            if (HeartSpriteBank.Has("crystalHeart_" + sideName)) {
                // this map has a custom heart registered: use it.
                return "crystalHeart_" + sideName;
            } else if (HeartSpriteBank.Has("crystalHeart_" + mapLevelSet)) {
                // this level set has a custom heart registered: use it.
                return "crystalHeart_" + mapLevelSet;
            }

            return null;
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
                    Dialog.Has(new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name + "_collabcredits") ||
                    Dialog.Has(new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name + "_collabcreditstags") ||
                    CollabModule.Instance.SaveData.SessionsPerLevel.ContainsKey(area.GetSID());
            }

            return orig(self, area);
        }

        private static int OnChapterPanelGetModeHeight(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
            // force the chapter panel to be bigger if deaths > 0 (we force deaths to display even if the player didn't beat the map) or if there is a speed berry PB,
            // because in these cases we have stuff to display in the chapter panel, and vanilla wouldn't display anything.
            AreaModeStats areaModeStats = self.RealStats.Modes[(int) self.Area.Mode];
            if (Engine.Scene == overworldWrapper?.Scene && !AreaData.Get(self.Area).Interlude_Safe
                && (areaModeStats.Deaths > 0 || CollabModule.Instance.SaveData.SpeedBerryPBs.ContainsKey(self.Area.GetSID()))) {

                return 540;
            }

            return orig(self);
        }

        private static void OnChapterPanelSwap(On.Celeste.OuiChapterPanel.orig_Swap orig, OuiChapterPanel self) {
            if (Engine.Scene != overworldWrapper?.Scene || (!gymSubmenuSelected(self)
                && !Dialog.Has(new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name + "_collabcredits")
                && !Dialog.Has(new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name + "_collabcreditstags")
                && !CollabModule.Instance.SaveData.SessionsPerLevel.ContainsKey(self.Area.GetSID()))) {

                // this isn't an in-game chapter panel, or there is no custom second page (no credits, no saved state, no gyms) => use vanilla
                orig(self);
                return;
            }

            DynData<OuiChapterPanel> data = new DynData<OuiChapterPanel>(self);
            bool selectingMode = data.Get<bool>("selectingMode");

            if (!selectingMode) {
                orig(self);
                return;
            }

            if (gymSubmenuSelected(self)) {
                data["gymTech"] = CollabMapDataProcessor.GymLevels[new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea").GetSID()].Tech;

                self.Focused = false;
                self.Overworld.ShowInputUI = !selectingMode;
                self.Add(new Coroutine(ChapterPanelSwapGymsRoutine(self, data)));
            } else {
                string areaName = new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea").Name;
                data["collabCredits"] = Dialog.Clean(areaName + "_collabcredits");
                data["collabCreditsTags"] = (areaName + "_collabcreditstags").DialogCleanOrNull();
                self.Focused = false;
                self.Overworld.ShowInputUI = !selectingMode;
                self.Add(new Coroutine(ChapterPanelSwapRoutine(self, data)));
            }
        }

        private static IEnumerator ChapterPanelSwapRoutine(OuiChapterPanel self, DynData<OuiChapterPanel> data) {
            float fromHeight = data.Get<float>("height");
            string forcedArea = new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name;
            int toHeight = Dialog.Has(forcedArea + "_collabcredits") ? 730 : (Dialog.Has(forcedArea + "_collabcreditstags") ? 450 : 300);

            data["resizing"] = true;
            m_PlayExpandSfx.Invoke(self, new object[] { fromHeight, (float) toHeight });

            float offset = 800f;
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                yield return null;
                data["contentOffset"] = new Vector2(440f + offset * Ease.CubeIn(p), data.Get<Vector2>("contentOffset").Y);
                data["height"] = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(p * 0.5f));
            }

            data["selectingMode"] = false;

            IList checkpoints = data.Get<IList>("checkpoints");
            checkpoints.Clear();

            bool hasContinueOption = CollabModule.Instance.SaveData.SessionsPerLevel.ContainsKey(self.Area.GetSID());

            checkpoints.Add(DynamicData.New(t_OuiChapterPanelOption)(new {
                Label = Dialog.Clean(hasContinueOption ? "collabutils2_chapterpanel_start" : "overworld_start", null),
                BgColor = Calc.HexToColor("eabe26"),
                Icon = GFX.Gui["areaselect/startpoint"],
                CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                Large = false,
                Siblings = hasContinueOption ? 2 : 1
            }));

            if (hasContinueOption) {
                checkpoints.Add(DynamicData.New(t_OuiChapterPanelOption)(new {
                    Label = Dialog.Clean("collabutils2_chapterpanel_continue", null),
                    Icon = GFX.Gui["areaselect/checkpoint"],
                    CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                    CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                    Large = false,
                    Siblings = 2,
                    CheckpointLevelName = "collabutils_continue"
                }));
            }

            data["option"] = hasContinueOption ? 1 : 0;

            for (int i = 0; i < checkpoints.Count; i++) {
                new DynamicData(checkpoints[i]).Invoke("SlideTowards", i, checkpoints.Count, true);
            }

            new DynamicData(checkpoints[hasContinueOption ? 1 : 0]).Set("Pop", 1f);
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                yield return null;
                data["height"] = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(Math.Min(1f, 0.5f + p * 0.5f)));
                data["contentOffset"] = new Vector2(440f + offset * (1f - Ease.CubeOut(p)), data.Get<Vector2>("contentOffset").Y);
            }

            data["contentOffset"] = new Vector2(440f, data.Get<Vector2>("contentOffset").Y);
            data["height"] = (float) toHeight;
            self.Focused = true;
            data["resizing"] = false;
        }

        private static IEnumerator ChapterPanelSwapGymsRoutine(OuiChapterPanel self, DynData<OuiChapterPanel> data) {
            float fromHeight = data.Get<float>("height");
            int toHeight = 730;

            data["resizing"] = true;
            m_PlayExpandSfx.Invoke(self, new object[] { fromHeight, (float) toHeight });

            float offset = 800f;
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                yield return null;
                data["contentOffset"] = new Vector2(440f + offset * Ease.CubeIn(p), data.Get<Vector2>("contentOffset").Y);
                data["height"] = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(p * 0.5f));
            }

            data["selectingMode"] = false;

            IList checkpoints = data.Get<IList>("checkpoints");
            checkpoints.Clear();

            string[] tech = data.Get<string[]>("gymTech").Where(name => CollabMapDataProcessor.GymTech.ContainsKey(name)).ToArray();
            foreach (string techName in tech) {
                CollabMapDataProcessor.GymTechInfo techInfo = CollabMapDataProcessor.GymTech[techName];
                var checkpoint = DynamicData.New(t_OuiChapterPanelOption)(new {
                    Label = Dialog.Clean($"{LobbyHelper.GetCollabNameForSID(techInfo.AreaSID)}_gym_{techName}_name", null),
                    BgColor = difficultyColors[techInfo.Difficulty],
                    Icon = GFX.Gui[$"CollabUtils2/areaselect/startpoint_{techInfo.Difficulty}"],
                    CheckpointLevelName = $"{techInfo.AreaSID}|{techInfo.Level}",
                    Large = false,
                    Siblings = tech.Count()
                });
                new DynamicData(checkpoint).Set("gymTechDifficulty", techInfo.Difficulty);
                checkpoints.Add(checkpoint);
            }

            data["option"] = 0;

            for (int i = 0; i < checkpoints.Count; i++) {
                new DynamicData(checkpoints[i]).Invoke("SlideTowards", i, checkpoints.Count, true);
            }

            new DynamicData(checkpoints[0]).Set("Pop", 1f);
            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                yield return null;
                data["height"] = MathHelper.Lerp(fromHeight, toHeight, Ease.CubeOut(Math.Min(1f, 0.5f + p * 0.5f)));
                data["contentOffset"] = new Vector2(440f + offset * (1f - Ease.CubeOut(p)), data.Get<Vector2>("contentOffset").Y);
            }

            data["contentOffset"] = new Vector2(440f, data.Get<Vector2>("contentOffset").Y);
            data["height"] = (float) toHeight;
            self.Focused = true;
            data["resizing"] = false;
        }

        private static void OnChapterPanelDrawCheckpoint(On.Celeste.OuiChapterPanel.orig_DrawCheckpoint orig, OuiChapterPanel self, Vector2 center, object option, int checkpointIndex) {
            if (overworldWrapper != null) {
                DynData<OuiChapterPanel> selfData = new DynData<OuiChapterPanel>(self);

                if (gymSubmenuSelected(self)) {
                    string[] collabTech = selfData.Get<string[]>("gymTech");
                    if (collabTech != null && collabTech.Length != 0) {
                        OnChapterPanelDrawGymCheckpoint(self, center, option, checkpointIndex, collabTech);
                        return;
                    }
                } else {
                    string collabCredits = selfData.Get<string>("collabCredits");
                    if (collabCredits != null) {
                        OnChapterPanelDrawCollabCreditsCheckpoint(self, center, checkpointIndex);
                        return;
                    }
                }
            }

            orig(self, center, option, checkpointIndex);
        }

        private static void OnChapterPanelDrawCollabCreditsCheckpoint(OuiChapterPanel self, Vector2 center, int checkpointIndex) {
            DynData<OuiChapterPanel> selfData = new DynData<OuiChapterPanel>(self);
            string collabCredits = selfData.Get<string>("collabCredits");

            if (checkpointIndex > 0) {
                return;
            }

            bool hasCredits = Dialog.Has(new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name + "_collabcredits");

            // panel height is 730 pixels when completely open, or 450 if there are only tags.
            // Tags should fade out quicker than text, because they are near the bottom of the panel, and it looks bad more quickly when the panel closes.
            float alphaText = Calc.ClampedMap(selfData.Get<float>("height"), 600, 730);
            float alphaTags = Calc.ClampedMap(selfData.Get<float>("height"), hasCredits ? 700 : 540, hasCredits ? 730 : 450);

            float heightTakenByTags = 0f;

            // draw tags.
            string collabCreditsTagsString = selfData.Get<string>("collabCreditsTags");
            if (collabCreditsTagsString != null) {
                // split on newlines to separate tags.
                string[] collabCreditsTags = collabCreditsTagsString.Split('\n');

                // split tags in lines, fitting as many tags as possible on each line.
                List<List<string>> lines = new List<List<string>>();
                List<float> lineWidths = new List<float>();

                // this block is responsible for splitting tags in lines.
                {
                    List<string> line = new List<string>();
                    float lineWidth = 0f;
                    for (int i = 0; i < collabCreditsTags.Length; i++) {
                        float advanceX = ActiveFont.Measure(collabCreditsTags[i].Trim()).X * 0.5f + 30f; // 30 = margin between tags
                        if (lineWidth + advanceX > 800f) {
                            // we exceeded the limit. we need a line break!
                            lines.Add(line.ToList());
                            lineWidths.Add(lineWidth);

                            line.Clear();
                            lineWidth = 0f;
                        }

                        // add the tag to the current line.
                        line.Add(collabCreditsTags[i].Trim());
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

                    foreach (string tag in lines[i]) {
                        // black edge > BaseColor text background > TextColor tag text
                        float width = ActiveFont.Measure(tag).X * 0.5f;
                        Draw.Rect(x - 10, y - 6, width + 20, 44, Color.Black * alphaTags);
                        Draw.Rect(x - 6, y - 2, width + 12, 36, self.Data.TitleBaseColor * alphaTags);
                        ActiveFont.Draw(tag, new Vector2(x, y), Vector2.Zero, Vector2.One * 0.5f, self.Data.TitleTextColor * alphaTags);

                        // advance the position to the next tag.
                        x += width + 30f;
                    }

                    // move up 1 line.
                    y -= 52f;
                    heightTakenByTags += 52f;
                }
            }

            // compute the maximum scale the credits can take (max 1) to fit the remaining space.
            Vector2 size = ActiveFont.Measure(collabCredits);
            float scale = Math.Min(1f, Math.Min((410f - heightTakenByTags) / size.Y, 800f / size.X));

            // draw the credits.
            ActiveFont.Draw(
                collabCredits,
                center + new Vector2(0f, 40f - heightTakenByTags / 2f),
                Vector2.One * 0.5f,
                Vector2.One * scale,
                Color.Black * 0.8f * alphaText
            );
        }

        private static void OnChapterPanelDrawGymCheckpoint(OuiChapterPanel self, Vector2 center, object option, int checkpointIndex, string[] collabTech) {
            AreaData forcedArea = new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea");

            if (CollabMapDataProcessor.GymTech.ContainsKey(collabTech[checkpointIndex])) {
                CollabMapDataProcessor.GymTechInfo techInfo = CollabMapDataProcessor.GymTech[collabTech[checkpointIndex]];

                string imageName = $"{LobbyHelper.GetCollabNameForSID(forcedArea.GetSID())}/Gyms/{collabTech[checkpointIndex]}";
                MTexture imagePreview = MTN.Checkpoints.Has(imageName) ? MTN.Checkpoints[imageName] : null;
                if (imagePreview != null) {
                    var optionData = new DynamicData(option);
                    Vector2 vector = center + (Vector2.UnitX * 800f * Ease.CubeIn(optionData.Get<float>("CheckpointSlideOut")));
                    imagePreview.DrawCentered(vector, Color.White, Vector2.One * 0.5f);
                }
            }
        }

        private static void ModOuiChapterPanelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // 1. Swap the "chapter xx" and the map name positions.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(-2f) || instr.MatchLdcR4(-18f))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel title position at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.EmitDelegate<Func<float, float>>(orig => {
                    if (Engine.Scene == overworldWrapper?.Scene && !isPanelShowingLobby()) {
                        return orig == -18f ? -49f : 43f;
                    } else {
                        return orig;
                    }
                });
            }

            cursor.Index = 0;

            // 2. Turn the chapter card silver or rainbow instead of gold when relevant.
            while (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdstr("areaselect/cardtop_golden") || instr.MatchLdstr("areaselect/card_golden"),
                instr => instr.MatchCall<OuiChapterPanel>("_ModCardTexture") || instr.MatchCall<OuiChapterPanel>("_ModAreaselectTexture"))) {

                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel card at {cursor.Index} in IL for OuiChapterPanel.Render");

                cursor.EmitDelegate<Func<string, string>>(orig => {
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
                });
            }

            cursor.Index = 0;

            // 3. If the author name is empty, center the map name like interludes.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchCallvirt<AreaData>("get_Interlude_Safe"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel title position at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<bool, OuiChapterPanel, bool>>((orig, self) => {
                    if (Engine.Scene == overworldWrapper?.Scene && new DynData<OuiChapterPanel>(self).Get<string>("chapter").Length == 0) {
                        return true; // interlude!
                    } else {
                        return orig;
                    }
                });
            }

            cursor.Index = 0;

            // 4. Keep forced area name even when it changes (for gyms)

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<AreaData>("Name"))) {
                Logger.Log("FlushelineCollab/InGameOverworldHelper", $"Modding chapter panel title name at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<string, OuiChapterPanel, string>>((name, self) => {
                    if (overworldWrapper != null) {
                        AreaData forcedArea = new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea");
                        if (forcedArea != null) {
                            return forcedArea.Name;
                        }
                    }
                    return name;
                });
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
            while (cursor.TryGotoNext(MoveType.Before,
                instr => instr.MatchLdarg(0),
                instr => instr.MatchCallvirt<OuiChapterPanel>("get_options"),
                instr => instr.MatchLdarg(0),
                instr => instr.MatchCallvirt<OuiChapterPanel>("get_option"),
                instr => true,
                instr => instr.MatchLdfld(t_OuiChapterPanelOption, "Label"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter option label position at {cursor.Index} in IL for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<OuiChapterPanel, bool>>(self => {
                    if (overworldWrapper != null) {
                        var selfData = new DynData<OuiChapterPanel>(self);
                        if (gymSubmenuSelected(self) && !selfData.Get<bool>("selectingMode")) {
                            var option = new DynamicData(selfData.Get<IList>("options")[selfData.Get<int>("option")]);
                            string difficulty = option.Get<string>("gymTechDifficulty");
                            if (difficulty != null) {
                                string difficultyLabel = Dialog.Clean($"collabutils2_difficulty_{difficulty}");
                                Vector2 renderPos = selfData.Get<Vector2>("OptionsRenderPosition");
                                ActiveFont.Draw(option.Get<string>("Label"), renderPos + new Vector2(0f, -140f), new Vector2(0.5f, 1f), Vector2.One * (1f + selfData.Get<Wiggler>("wiggler").Value * 0.1f), Color.Black * 0.8f);
                                ActiveFont.Draw(difficultyLabel, renderPos + new Vector2(0f, -140f), new Vector2(0.5f, 0f), Vector2.One * 0.6f * (1f + selfData.Get<Wiggler>("wiggler").Value * 0.1f), Color.Black * 0.8f);
                                return true;
                            }
                        }
                    }
                    return false;
                });

                cursor.Emit(OpCodes.Brtrue, afterOptionLabel);
                cursor.Index++;
            }
        }

        private static IEnumerator OnJournalEnter(On.Celeste.OuiJournal.orig_Enter orig, OuiJournal self, Oui from) {
            IEnumerator origc = orig(self, from);

            SaveData save = SaveData.Instance;
            AreaData forceArea = new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea");
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

            DeathsCounter deathsCounter = new DynData<OuiChapterPanel>(self).Get<DeathsCounter>("deaths");

            if (Engine.Scene == overworldWrapper?.Scene) {
                // within lobbies, death counts always show up, even if you didn't beat the map yet.
                AreaModeStats areaModeStats = self.DisplayedStats.Modes[(int) self.Area.Mode];
                deathsCounter.Visible = areaModeStats.Deaths > 0 && !AreaData.Get(self.Area).Interlude_Safe;
            }

            // mod the death icon: for the path, use the current level set, or for lobbies, the lobby's matching level set.
            string pathToSkull = "CollabUtils2/skulls/" + self.Area.GetLevelSet();
            string lobbyLevelSet = LobbyHelper.GetLobbyLevelSet(self.Area.GetSID());
            if (lobbyLevelSet != null) {
                pathToSkull = "CollabUtils2/skulls/" + lobbyLevelSet;
            }
            if (GFX.Gui.Has(pathToSkull)) {
                new DynData<DeathsCounter>(deathsCounter)["icon"] = GFX.Gui[pathToSkull];
            }
            new DynData<DeathsCounter>(deathsCounter)["modifiedByCollabUtils"] = GFX.Gui.Has(pathToSkull);


            if (isPanelShowingLobby(self) || Engine.Scene == overworldWrapper?.Scene) {
                // turn strawberry counter into golden if there only are golden berries in the map
                MapData mapData = AreaData.Get(self.Area).Mode[0].MapData;
                if (mapData.GetDetectedStrawberriesIncludingUntracked() == mapData.Goldenberries.Count) {
                    StrawberriesCounter strawberriesCounter = new DynData<OuiChapterPanel>(self).Get<StrawberriesCounter>("strawberries");
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
                cursor.EmitDelegate<Func<float, DeathsCounter, MTexture, float>>((orig, self, icon) => {
                    DynData<DeathsCounter> data = new DynData<DeathsCounter>(self);
                    if (data.Data.ContainsKey("modifiedByCollabUtils") && data.Get<bool>("modifiedByCollabUtils")) {
                        return icon.Width - 4; // vanilla icons are 66px wide.
                    }
                    return orig;
                });
            }
        }

        private static void ModStrawberriesCounterRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("collectables/goldberry"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Changing strawberry icon w/ silver one at {cursor.Index} in IL for StrawberriesCounter.Render");
                cursor.EmitDelegate<Func<string, string>>(orig => {
                    string sid = getCurrentPanelMapSID();
                    if (CollabMapDataProcessor.MapsWithRainbowBerries.Contains(sid)) {
                        return "CollabUtils2/rainbowberry";
                    }
                    if (CollabMapDataProcessor.MapsWithSilverBerries.Contains(sid)) {
                        return "CollabUtils2/silverberry";
                    }
                    return orig;
                });
            }
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
                    DynData<Overworld> overworldData = new DynData<Overworld>(overworld);
                    overworldData.Set("returnToLobbyMode", returnToLobbyMode);
                    overworldData.Set("saveAndReturnToLobbyAllowed", savingAllowed);
                    overworldData.Set("exitFromGym", exitFromGym);
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
                }
            };

            level.Add(overworldWrapper);
            new DynData<Overworld>(overworldWrapper.WrappedScene).Set("collabInGameForcedArea", area);
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
            string sid = panel.Area.GetSID();
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
            return LobbyHelper.IsCollabLobby(panel?.Area.GetSID() ?? "");
        }

        private static bool gymSubmenuSelected(OuiChapterPanel panel = null) {
            panel = panel ?? getChapterPanel();
            return panel != null && (new DynData<Overworld>(panel.Overworld).Data.ContainsKey("gymExitMapSID") ||
                (panel.Area.Mode == AreaMode.BSide && CollabMapDataProcessor.GymLevels.ContainsKey(
                new DynData<Overworld>(panel.Overworld).Get<AreaData>("collabInGameForcedArea").GetSID())));
        }

        private static bool returnToLobbySelected(OuiChapterPanel panel = null) {
            panel = panel ?? getChapterPanel();
            return panel != null && panel.Area.Mode == AreaMode.CSide
                && new DynData<Overworld>(panel.Overworld).Data.TryGetValue("exitFromGym", out object val) && (bool) val;
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
                cursor.EmitDelegate<Func<float, OuiChapterPanel, float>>((orig, self) => {
                    if (Engine.Scene == overworldWrapper?.Scene) {
                        string mapAuthor = DynamicData.For(self).Get<string>("chapter");
                        if (mapAuthor?.Length != 0) {
                            // if the map has author, use the wider one between it and the map title
                            float width = ActiveFont.Measure(mapAuthor).X * 0.6f;
                            return Math.Max(orig, width);
                        }
                    }

                    return orig;
                });
            }
        }

        // ModInterop exports

        [ModExportName("CollabUtils2.InGameOverworldHelper")]
        private static class ModExports {
            public static SpriteBank GetHeartSpriteBank() {
                return HeartSpriteBank;
            }
            public static string GetGuiHeartSpriteId(string mapSID, AreaMode side) {
                return InGameOverworldHelper.GetGuiHeartSpriteId(mapSID, side);
            }
        }
    }
}
