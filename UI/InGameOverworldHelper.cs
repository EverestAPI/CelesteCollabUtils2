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

        private static SpriteBank heartSpriteBank;

        private static bool skipSetMusic;
        private static bool skipSetAmbience;

        private static AreaKey? lastArea;

        private static readonly Type t_OuiChapterPanelOption = typeof(OuiChapterPanel)
            .GetNestedType("Option", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        private static readonly ConstructorInfo c_OuiChapterPanelOption = t_OuiChapterPanelOption
            .GetConstructor(Type.EmptyTypes);
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
            IL.Celeste.OuiChapterPanel.Render += ModOuiChapterPanelEnter;
            IL.Celeste.DeathsCounter.Render += ModDeathsCounterRender;
            IL.Celeste.StrawberriesCounter.Render += ModStrawberriesCounterRender;
            On.Celeste.MapData.Load += ModMapDataLoad;
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
            IL.Celeste.OuiChapterPanel.Render -= ModOuiChapterPanelEnter;
            IL.Celeste.DeathsCounter.Render -= ModDeathsCounterRender;
            IL.Celeste.StrawberriesCounter.Render -= ModStrawberriesCounterRender;
            On.Celeste.MapData.Load -= ModMapDataLoad;
        }

        public static void LoadContent() {
            heartSpriteBank = new SpriteBank(GFX.Gui, "Graphics/CollabUtils2/CrystalHeartSwaps.xml");
        }

        private static void OnPause(Level level, int startIndex, bool minimal, bool quickReset) {
            if (overworldWrapper != null) {
                Close(level, true, true);
            }
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
            AreaData forceArea = self.Overworld == null ? null : new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea");
            if (forceArea == null) {
                orig(self);
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

            DynData<OuiChapterPanel> data = new DynData<OuiChapterPanel>(self);
            data["hasCollabCredits"] = true;

            data["chapter"] = Dialog.Clean(new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea").Name + "_author");

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

        private static bool OnSaveDataFoundAnyCheckpoints(On.Celeste.SaveData.orig_FoundAnyCheckpoints orig, SaveData self, AreaKey area) {
            if (Engine.Scene == overworldWrapper?.Scene)
                return true;

            return orig(self, area);
        }

        private static int OnChapterPanelGetModeHeight(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
            AreaModeStats areaModeStats = self.RealStats.Modes[(int) self.Area.Mode];
            if (Engine.Scene == overworldWrapper?.Scene && areaModeStats.Deaths > 0)
                return 540;

            return orig(self);
        }

        private static void OnChapterPanelSwap(On.Celeste.OuiChapterPanel.orig_Swap orig, OuiChapterPanel self) {
            if (Engine.Scene != overworldWrapper?.Scene) {
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

        private static void ModOuiChapterPanelEnter(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(-2f) || instr.MatchLdcR4(-18f))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel title position at {cursor.Index} in IL for OuiChapterPanel.Render");
                cursor.EmitDelegate<Func<float, float>>(orig => {
                    if (Engine.Scene == overworldWrapper?.Scene) {
                        return orig == -18f ? -49f : 43f;
                    } else {
                        return orig;
                    }
                });
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("areaselect/cardtop_golden") || instr.MatchLdstr("areaselect/card_golden"))) {
                Logger.Log("CollabUtils2/InGameOverworldHelper", $"Modding chapter panel card at {cursor.Index} in IL for OuiChapterPanel.Render");

                cursor.EmitDelegate<Func<string, string>>(orig => {
                    if (Engine.Scene == overworldWrapper?.Scene) {
                        return orig == "areaselect/cardtop_golden" ? "CollabUtils2/chapterCard/cardtop_silver" : "CollabUtils2/chapterCard/card_silver";
                    }
                    if (isPanelShowingLobby()) {
                        return orig == "areaselect/cardtop_golden" ? "CollabUtils2/chapterCard/cardtop_rainbow" : "CollabUtils2/chapterCard/card_rainbow";
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
                deathsCounter.Visible = areaModeStats.Deaths > 0;

                // mod the death icon
                string pathToSkull = "CollabUtils2/skulls/" + self.Area.GetLevelSet();
                if (GFX.Gui.Has(pathToSkull)) {
                    new DynData<DeathsCounter>(deathsCounter)["icon"] = GFX.Gui[pathToSkull];
                }
            }


            if (isPanelShowingLobby() || Engine.Scene == overworldWrapper?.Scene) {
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
                    if (Engine.Scene == overworldWrapper?.Scene) {
                        return "CollabUtils2/silverberry";
                    }
                    if (isPanelShowingLobby()) {
                        return "CollabUtils2/rainbowberry";
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
            Open(player, AreaData.Get(sid) ?? AreaData.Get(0), out OuiHelper_EnterChapterPanel.Start,
                overworld => {
                    new DynData<Overworld>(overworld).Set("returnToLobbyMode", returnToLobbyMode);
                    OuiChapterPanel panel = overworld.GetUI<OuiChapterPanel>();

                    // customize heart gem icon
                    string animId = "crystalHeart_" + AreaData.Get(sid)?.GetLevelSet()?.DialogKeyify();
                    if (heartSpriteBank.Has(animId)) {
                        Sprite heartSprite = heartSpriteBank.Create(animId);
                        new DynData<OuiChapterPanel>(panel).Get<HeartGemDisplay>("heart").Sprites[0] = heartSprite;
                        heartSprite.Play("spin");
                    }
                });
        }

        public static void OpenJournal(Player player, string levelset) {
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

        private static bool isPanelShowingLobby() {
            return LobbyHelper.GetLobbyLevelSet((Engine.Scene as Overworld)?.GetUI<OuiChapterPanel>()?.Area.GetSID() ?? "") != null;
        }
    }
}
