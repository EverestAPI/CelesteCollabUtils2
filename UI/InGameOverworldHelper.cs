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
            IL.Celeste.OuiChapterPanel.Render += ModOuiChapterPanelEnter;
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
            IL.Celeste.OuiChapterPanel.Render -= ModOuiChapterPanelEnter;
        }

        private static void OnPause(Level level, int startIndex, bool minimal, bool quickReset) {
            Close(level, true, true);
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
            if (Engine.Scene == overworldWrapper?.Scene && (int) self.Area.Mode >= 1)
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

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, OuiChapterPanel, float>>(moveAroundPanelHeader);
            }
        }

        private static float moveAroundPanelHeader(float orig, OuiChapterPanel self) {
            AreaData forceArea = self.Overworld == null ? null : new DynData<Overworld>(self.Overworld).Get<AreaData>("collabInGameForcedArea");
            if (forceArea != null) {
                return orig == -18f ? -49f : 43f;
            } else {
                return orig;
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

        private static IEnumerator UpdateIconRoutine(OuiChapterPanel panel, OuiChapterSelectIcon icon) {
            Overworld overworld = overworldWrapper?.WrappedScene;
            if (overworld == null)
                yield break;

            while (overworld.Current == panel || overworld.Last == panel || overworld.Next == panel) {
                icon.Position = panel.Position + panel.IconOffset;
                yield return null;
            }
        }

        public static void OpenChapterPanel(Player player, string sid) {
            Open(player, AreaData.Get(sid) ?? AreaData.Get(0), out OuiHelper_EnterChapterPanel.Start);
        }

        public static void OpenJournal(Player player, string levelset) {
            Open(player, AreaData.Areas.FirstOrDefault(area => area.LevelSet == levelset) ?? AreaData.Get(0), out OuiHelper_EnterJournal.Start);
        }

        public static void Open(Player player, AreaData area, out bool opened) {
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

    }
}
