using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/ChapterPanelTrigger")]
    public class ChapterPanelTrigger : Trigger {

        public string map;

        private static SceneWrappingEntity<Overworld> overworldWrapper;

        private static bool skipSetMusic;
        private static bool skipSetAmbience;

        private TalkComponent talkComponent;

        public ChapterPanelTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            map = data.Attr("map");

            Add(talkComponent = new TalkComponent(
                new Rectangle(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? (data.Nodes[0] - data.Position) : new Vector2(data.Width / 2f, data.Height / 2f),
                Interact
            ) { PlayerMustBeFacing = false });
        }

        public static void Load() {
            Everest.Events.Level.OnPause += OnPause;
            On.Celeste.Audio.SetMusic += OnSetMusic;
            On.Celeste.Audio.SetAmbience += OnSetAmbience;
        }

        public static void Unload() {
            Everest.Events.Level.OnPause -= OnPause;
            On.Celeste.Audio.SetMusic -= OnSetMusic;
            On.Celeste.Audio.SetAmbience -= OnSetAmbience;
        }

        private static void OnPause(Level level, int startIndex, bool minimal, bool quickReset) {
            Uninteract(level, true, true);
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

        public void Interact(Player player) {
            if (overworldWrapper?.Scene == Engine.Scene || player.StateMachine.State == Player.StDummy)
                return;
            player.StateMachine.State = Player.StDummy;

            OuiHelper_EnterChapterPanel.Start = true;
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
                overworld.RendererList._UpdateLists();
            };

            Scene.Add(overworldWrapper);
        }

        public static void Uninteract(Level level, bool removeScene, bool resetPlayer) {
            if (removeScene) {
                overworldWrapper?.RemoveSelf();
                overworldWrapper = null;
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

        public override void Update() {
            base.Update();

            Level level = Scene as Level;

            Overworld overworld = overworldWrapper?.WrappedScene;
            if (overworld != null) {
                if (overworld.Next is OuiChapterSelect) {
                    overworld.Next.RemoveSelf();
                    Uninteract(level, false, true);
                }

                overworld.Snow.ParticleAlpha = 0.25f;

                if (overworld.Current != null || overworld.Next?.Scene != null) {
                    overworld.Snow.Alpha = Calc.Approach(overworld.Snow.Alpha, 1f, Engine.DeltaTime * 2f);

                } else {
                    talkComponent.Enabled = false;
                    overworld.Snow.Alpha = Calc.Approach(overworld.Snow.Alpha, 0, Engine.DeltaTime * 2f);
                    if (overworld.Snow.Alpha <= 0.01f) {
                        talkComponent.Enabled = true;
                        Uninteract(level, true, true);
                    }
                }
            }
        }

    }
}
