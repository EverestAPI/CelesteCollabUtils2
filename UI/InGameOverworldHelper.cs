using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
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

        public static void Load() {
            Everest.Events.Level.OnPause += OnPause;
            On.Celeste.Audio.SetMusic += OnSetMusic;
            On.Celeste.Audio.SetAmbience += OnSetAmbience;
            On.Celeste.OuiChapterPanel.Reset += OnChapterPanelReset;
            On.Celeste.OuiJournal.Enter += OnJournalEnter;
        }

        public static void Unload() {
            Everest.Events.Level.OnPause -= OnPause;
            On.Celeste.Audio.SetMusic -= OnSetMusic;
            On.Celeste.Audio.SetAmbience -= OnSetAmbience;
            On.Celeste.OuiChapterPanel.Reset -= OnChapterPanelReset;
            On.Celeste.OuiJournal.Enter -= OnJournalEnter;
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
            AreaData forceArea = new DynamicData(self.Overworld).Get<AreaData>("areaForcedByInGameOverworldHelper");
            if (forceArea == null) {
                orig(self);
                return;
            }

            SaveData save = SaveData.Instance;
            Session session = save.CurrentSession;
            lastArea = save.LastArea;

            save.LastArea = forceArea.ToKey();
            save.CurrentSession = null;

            List<OuiChapterSelectIcon> icons = self.Overworld.Entities.FindAll<OuiChapterSelectIcon>();
            OuiChapterSelectIcon icon = icons[save.LastArea.ID];
            icon.SnapToSelected();
            icon.Add(new Coroutine(UpdateIconRoutine(self, icon)));

            orig(self);

           new DynamicData(self).Get<IList>("modes").Add(
                new DynamicData(c_OuiChapterPanelOption.Invoke(new object[0])) {
                    { "Label", "Uhh" },
                    { "Icon", GFX.Gui["areas/null"] },
                    { "ID", "C" }
                }.Target
            );

            // LastArea is also checked in Render.
            save.CurrentSession = session;
        }

        private static IEnumerator OnJournalEnter(On.Celeste.OuiJournal.orig_Enter orig, OuiJournal self, Oui from) {
            IEnumerator origc = orig(self, from);

            SaveData save = SaveData.Instance;
            AreaData forceArea = new DynamicData(self.Overworld).Get<AreaData>("areaForcedByInGameOverworldHelper");
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

            player.Scene.Add(overworldWrapper);
            new DynamicData(overworldWrapper.WrappedScene).Set("areaForcedByInGameOverworldHelper", area);

            overworldWrapper.Add(new Coroutine(UpdateRoutine()));
        }

        public static void Close(Level level, bool removeScene, bool resetPlayer) {
            if (removeScene) {
                overworldWrapper?.RemoveSelf();
                overworldWrapper = null;

                if (lastArea != null && SaveData.Instance != null) {
                    SaveData.Instance.LastArea = lastArea.Value;
                    lastArea = null;
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
