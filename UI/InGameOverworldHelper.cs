using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
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

        private static bool skipSetMusic;
        private static bool skipSetAmbience;

        private static AreaKey? lastArea;

        private static readonly Type t_OuiChapterPanelOption = typeof(OuiChapterPanel)
            .GetNestedType("Option", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        private static MethodInfo m_PlayExpandSfx = typeof(OuiChapterPanel)
            .GetMethod("PlayExpandSfx", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Load() {
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
            On.Celeste.MapData.Load += ModMapDataLoad;
            On.Celeste.OuiChapterPanel.Start += OnOuiChapterPanelStart;
            On.Celeste.Player.Die += OnPlayerDie;
            On.Celeste.Mod.AssetReloadHelper.ReloadLevel += OnReloadLevel;
        }

        public static void Unload() {
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
            On.Celeste.MapData.Load -= ModMapDataLoad;
            On.Celeste.OuiChapterPanel.Start -= OnOuiChapterPanelStart;
            On.Celeste.Player.Die -= OnPlayerDie;
            On.Celeste.Mod.AssetReloadHelper.ReloadLevel -= OnReloadLevel;
        }

        private static void OnOuiChapterPanelStart(On.Celeste.OuiChapterPanel.orig_Start orig, OuiChapterPanel self, string checkpoint) {
            if (overworldWrapper != null) {
                (overworldWrapper.Scene as Level).PauseLock = true;
            }

            orig(self, checkpoint);
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
                Level level = Engine.Scene as Level;
                if (level == null) {
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

        private static bool OnSetMusic(On.Celeste.Audio.orig_SetMusic orig, string path, bool startPlaying, bool allowFadeOut) {
            if (skipSetMusic) {
                skipSetMusic = false;
                return false;
            }

            return orig(path, startPlaying, allowFadeOut);
        }

        private static bool OnSetAmbience(On.Celeste.Audio.orig_SetAmbience orig, string path, bool startPlaying) {
            if (skipSetAmbience) {
                skipSetAmbience = false;
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
                data["chapter"] = Dialog.Clean(new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea").Name + "_author");
            }

            /*
            (data.modes as IList).Add(
                DynamicData.New(t_OuiChapterPanelOption)(new {
                    Label = "",
                    BgColor = Calc.HexToColor("223022"),
                    Icon = GFX.Gui["areas/null"],
                    Large = false
                })
            );
            */

            // LastArea is also checked in Render.
            save.CurrentSession = session;
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
            string animId = null;

            string sid = panel.Area.GetSID();
            string mapName = sid.DialogKeyify();
            string mapLevelSet = AreaData.Get(sid)?.GetLevelSet().DialogKeyify();

            if (HeartSpriteBank.Has("crystalHeart_" + mapName)) {
                // this map has a custom heart registered: use it.
                animId = "crystalHeart_" + mapName;
            } else if (HeartSpriteBank.Has("crystalHeart_" + mapLevelSet)) {
                // this level set has a custom heart registered: use it.
                animId = "crystalHeart_" + mapLevelSet;
            }

            if (animId != null) {
                Sprite heartSprite = HeartSpriteBank.Create(animId);
                new DynData<OuiChapterPanel>(panel).Get<HeartGemDisplay>("heart").Sprites[0] = heartSprite;
                heartSprite.Play("spin");
                new DynData<OuiChapterPanel>(panel)["heartDirty"] = true;
            }
        }

        private static bool OnSaveDataFoundAnyCheckpoints(On.Celeste.SaveData.orig_FoundAnyCheckpoints orig, SaveData self, AreaKey area) {
            // if this is a collab chapter panel, display the second page (containing the credits) if they are defined in English.txt.
            if (Engine.Scene == overworldWrapper?.Scene)
                return orig(self, area) || Dialog.Has(new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name + "_collabcredits");

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
            if (Engine.Scene != overworldWrapper?.Scene || !Dialog.Has(new DynData<Overworld>(overworldWrapper.WrappedScene).Get<AreaData>("collabInGameForcedArea").Name + "_collabcredits")) {
                orig(self);
                return;
            }

            DynData<OuiChapterPanel> data = new DynData<OuiChapterPanel>(self);
            bool selectingMode = data.Get<bool>("selectingMode");
            if ((int) self.Area.Mode >= 1 && selectingMode) {
                return;
            }

            if (!selectingMode) {
                orig(self);
                return;
            }

            data["collabCredits"] = Dialog.Clean(new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea").Name + "_collabcredits");
            self.Focused = false;
            self.Overworld.ShowInputUI = !selectingMode;
            self.Add(new Coroutine(ChapterPanelSwapRoutine(self, data)));
        }

        private static IEnumerator ChapterPanelSwapRoutine(OuiChapterPanel self, DynData<OuiChapterPanel> data) {
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

            checkpoints.Add(DynamicData.New(t_OuiChapterPanelOption)(new {
                Label = Dialog.Clean("overworld_start", null),
                BgColor = Calc.HexToColor("eabe26"),
                Icon = GFX.Gui["areaselect/startpoint"],
                CheckpointRotation = Calc.Random.Choose(-1, 1) * Calc.Random.Range(0.05f, 0.2f),
                CheckpointOffset = new Vector2(Calc.Random.Range(-16, 16), Calc.Random.Range(-16, 16)),
                Large = false
            }));

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
            string collabCredits = new DynData<OuiChapterPanel>(self).Get<string>("collabCredits");
            if (collabCredits == null) {
                orig(self, center, option, checkpointIndex);
                return;
            }

            if (checkpointIndex > 0) {
                return;
            }

            Vector2 size = ActiveFont.Measure(collabCredits);
            float scale = Math.Min(1f, Math.Min(410f / size.Y, 800f / size.X));

            ActiveFont.Draw(
                collabCredits,
                center + new Vector2(0f, 40f),
                Vector2.One * 0.5f,
                Vector2.One * scale,
                Color.Black * 0.8f
            );
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

            // 2. Resize the title if it does not fit.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(-60))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel title bookmark position at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, OuiChapterPanel, float>>((orig, self) => {
                    if (Engine.Scene == overworldWrapper?.Scene) {
                        float mapNameSize = ActiveFont.Measure(Dialog.Clean(AreaData.Get(self.Area).Name)).X;
                        return orig - Math.Max(0f, mapNameSize - 550f);
                    } else {
                        return orig;
                    }
                });
            }

            cursor.Index = 0;

            // 3. Turn the chapter card silver or rainbow instead of gold when relevant.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("areaselect/cardtop_golden") || instr.MatchLdstr("areaselect/card_golden"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel card at {cursor.Index} in IL for OuiChapterPanel.Render");

                cursor.EmitDelegate<Func<string, string>>(orig => {
                    if (isPanelShowingLobby()) {
                        return orig == "areaselect/cardtop_golden" ? "CollabUtils2/chapterCard/cardtop_rainbow" : "CollabUtils2/chapterCard/card_rainbow";
                    }
                    if (Engine.Scene == overworldWrapper?.Scene && !LobbyHelper.IsHeartSide(overworldWrapper.WrappedScene?.GetUI<OuiChapterPanel>().Area.GetSID())) {
                        return orig == "areaselect/cardtop_golden" ? "CollabUtils2/chapterCard/cardtop_silver" : "CollabUtils2/chapterCard/card_silver";
                    }
                    return orig;
                });
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

            if (forceArea != null) {
                save.LastArea = lastArea.Value;
                lastArea = null;
            }
        }


        private static void OnChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle,
            bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {

            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            if (Engine.Scene == overworldWrapper?.Scene) {
                AreaModeStats areaModeStats = self.DisplayedStats.Modes[(int) self.Area.Mode];
                DeathsCounter deathsCounter = new DynData<OuiChapterPanel>(self).Get<DeathsCounter>("deaths");
                deathsCounter.Visible = areaModeStats.Deaths > 0 && !AreaData.Get(self.Area).Interlude_Safe;

                // mod the death icon
                string pathToSkull = "CollabUtils2/skulls/" + self.Area.GetLevelSet();
                if (GFX.Gui.Has(pathToSkull)) {
                    new DynData<DeathsCounter>(deathsCounter)["icon"] = GFX.Gui[pathToSkull];
                }
            }


            if (isPanelShowingLobby(self) || Engine.Scene == overworldWrapper?.Scene) {
                // turn strawberry counter into golden if there is no berry in the map
                if (AreaData.Get(self.Area).Mode[0].TotalStrawberries == 0) {
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
                cursor.Emit(OpCodes.Ldfld, typeof(DeathsCounter).GetField("icon", BindingFlags.NonPublic | BindingFlags.Instance));
                cursor.EmitDelegate<Func<float, MTexture, float>>((orig, icon) => {
                    if (Engine.Scene == overworldWrapper?.Scene) {
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
                    if (isPanelShowingLobby()) {
                        return "CollabUtils2/rainbowberry";
                    }
                    if (Engine.Scene == overworldWrapper?.Scene && !LobbyHelper.IsHeartSide(overworldWrapper.WrappedScene?.GetUI<OuiChapterPanel>().Area.GetSID())) {
                        return "CollabUtils2/silverberry";
                    }
                    return orig;
                });
            }
        }

        private static void ModMapDataLoad(On.Celeste.MapData.orig_Load orig, MapData self) {
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

        public static void OpenChapterPanel(Player player, string sid, ChapterPanelTrigger.ReturnToLobbyMode returnToLobbyMode) {
            player.Drop();
            Open(player, AreaData.Get(sid) ?? AreaData.Get(0), out OuiHelper_EnterChapterPanel.Start,
                overworld => new DynData<Overworld>(overworld).Set("returnToLobbyMode", returnToLobbyMode));
        }

        public static void OpenJournal(Player player, string levelset) {
            player.Drop();
            Open(player, AreaData.Areas.FirstOrDefault(area => area.LevelSet == levelset) ?? AreaData.Get(0), out OuiHelper_EnterJournal.Start);
        }

        public static void Open(Player player, AreaData area, out bool opened, Action<Overworld> callback = null) {
            opened = false;

            if (overworldWrapper?.Scene == Engine.Scene || player.StateMachine.State == Player.StDummy)
                return;
            player.StateMachine.State = Player.StDummy;

            opened = true;

            skipSetMusic = true;
            skipSetAmbience = true;

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

            overworldWrapper.Add(new Coroutine(UpdateRoutine()));
        }

        public static void Close(Level level, bool removeScene, bool resetPlayer) {
            if (removeScene) {
                overworldWrapper?.RemoveSelf();
                overworldWrapper = null;

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

        private static IEnumerator UpdateRoutine() {
            Level level = overworldWrapper.Scene as Level;
            Overworld overworld = overworldWrapper.WrappedScene;

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
        }

        private static bool isPanelShowingLobby(OuiChapterPanel panel = null) {
            if (overworldWrapper != null) {
                panel = overworldWrapper.WrappedScene?.GetUI<OuiChapterPanel>();
            }
            if (panel == null) {
                panel = (Engine.Scene as Overworld)?.GetUI<OuiChapterPanel>();
            }
            return LobbyHelper.GetLobbyLevelSet(panel?.Area.GetSID() ?? "") != null;
        }
    }
}
