using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity(CHAPTER_PANEL_TRIGGER_NAME, EXIT_FROM_GYM_TRIGGER_NAME)]
    public class ChapterPanelTrigger : Trigger {
        public const string CHAPTER_PANEL_TRIGGER_NAME = "CollabUtils2/ChapterPanelTrigger";
        public const string EXIT_FROM_GYM_TRIGGER_NAME = "CollabUtils2/ExitFromGymTrigger";
        
        public enum ReturnToLobbyMode {
            SetReturnToHere, RemoveReturn, DoNotChangeReturn
        }

        private readonly TalkComponent talkComponent;

        public ChapterPanelTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {

            string map = data.Attr("map");
            ReturnToLobbyMode returnToLobbyMode = data.Enum("returnToLobbyMode", ReturnToLobbyMode.SetReturnToHere);
            bool savingAllowed = data.Bool("allowSaving", defaultValue: true);
            bool exitFromGym = (data.Name == EXIT_FROM_GYM_TRIGGER_NAME);

            Add(talkComponent = new TalkComponent(
                new Rectangle(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? (data.Nodes[0] - data.Position) : new Vector2(data.Width / 2f, data.Height / 2f),
                player => InGameOverworldHelper.OpenChapterPanel(player,
                    exitFromGym ? CollabModule.Instance.Session.GymExitMapSID : map,
                    exitFromGym ? ReturnToLobbyMode.DoNotChangeReturn : returnToLobbyMode,
                    exitFromGym ? CollabModule.Instance.Session.GymExitSaveAllowed : savingAllowed,
                    exitFromGym)
            ) { PlayerMustBeFacing = false });
        }

        public override void Update() {
            base.Update();
            talkComponent.Enabled = !InGameOverworldHelper.IsOpen;
        }
    }
}
