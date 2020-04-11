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

        private float timeout = 0f;

        public ChapterPanelTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            map = data.Attr("map");

            Add(new TalkComponent(
                new Rectangle(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? (data.Nodes[0] - data.Position) : new Vector2(data.Width / 2f, data.Height / 2f),
                Interact
            ) { PlayerMustBeFacing = false });
        }

        public static void Load() {
            Everest.Events.Level.OnPause += OnPause;
        }

        public static void Unload() {
            Everest.Events.Level.OnPause -= OnPause;
        }

        private static void OnPause(Level level, int startIndex, bool minimal, bool quickReset) {
            Uninteract(level);
        }

        public void Interact(Player player) {
            if (timeout > 0f || player.StateMachine.State == Player.StDummy)
                return;
            player.StateMachine.State = Player.StDummy;

            OuiHelper_EnterChapterPanel.Start = true;
            HiresSnow snow = new HiresSnow();
            snow.Alpha = 0f;
            overworldWrapper = new SceneWrappingEntity<Overworld>(new Overworld(new OverworldLoader((Overworld.StartMode) (-1), snow)));
            overworldWrapper.OnBegin += (overworld) => {
                overworld.RendererList.Remove(overworld.RendererList.Renderers.Find(r => r is MountainRenderer));
                overworld.RendererList.Remove(overworld.RendererList.Renderers.Find(r => r is ScreenWipe));
                overworld.RendererList._UpdateLists();
            };

            Scene.Add(overworldWrapper);
        }

        public static void Uninteract(Level level) {
            overworldWrapper?.RemoveSelf();
            overworldWrapper = null;

            Player player = level.Tracker.GetEntity<Player>();
            if (player == null || player.StateMachine.State != Player.StDummy)
                return;

            Engine.Scene.OnEndOfFrame += () => {
                player.StateMachine.State = Player.StNormal;
            };
        }

        public override void Update() {
            base.Update();

            Overworld overworld = overworldWrapper?.WrappedScene;
            if (overworld != null) {
                overworld.Snow.Alpha = Calc.Approach(overworld.Snow.Alpha, 1f, Engine.DeltaTime * 2f);
                overworld.Snow.ParticleAlpha = Calc.Approach(overworld.Snow.Alpha, 1f, Engine.DeltaTime * 2f);

                if (overworld.Next is OuiChapterSelect) {
                    Uninteract(Scene as Level);
                    timeout = 0.1f;
                }
            }

            timeout -= Engine.DeltaTime;
            if (timeout < 0f)
                timeout = 0f;
        }

    }
}
