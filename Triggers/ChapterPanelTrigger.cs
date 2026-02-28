using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/ChapterPanelTrigger", "CollabUtils2/ExitFromGymTrigger")]
    public class ChapterPanelTrigger : Trigger {
        public enum ReturnToLobbyMode {
            SetReturnToHere, RemoveReturn, DoNotChangeReturn
        }

        private readonly TalkComponent talkComponent;

        public ChapterPanelTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {

            string map = data.Attr("map");
            ReturnToLobbyMode returnToLobbyMode = data.Enum("returnToLobbyMode", ReturnToLobbyMode.SetReturnToHere);
            bool savingAllowed = data.Bool("allowSaving", defaultValue: true);
            bool exitFromGym = data.Name == "CollabUtils2/ExitFromGymTrigger";

            Add(talkComponent = new TalkComponent(
                new Rectangle(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? (data.Nodes[0] - data.Position) : new Vector2(data.Width / 2f, data.Height / 2f),
                player => {
                    Session session = SceneAs<Level>().Session;
                    string sid = session.Area.SID;
                    string level = session.Level;
                    
                    string collabID = LobbyHelper.GetCollabNameForSID(sid);
                    if (exitFromGym && CollabMapDataProcessor.GymTech.TryGetValue(collabID, out Dictionary<string, CollabMapDataProcessor.GymTechInfo> techForCollab)) {
                        string currentGymTech = techForCollab.FirstOrDefault(kvp => kvp.Value.AreaSID == sid && kvp.Value.Level == level).Key;
                        if (currentGymTech is not null) {
                            if (CollabModule.Instance.SaveData.LearntTech.TryGetValue(collabID, out HashSet<string> learntTech))
                                learntTech.Add(currentGymTech);
                            else
                                CollabModule.Instance.SaveData.LearntTech.Add(collabID, [currentGymTech]);
                        }
                    }
                    
                    InGameOverworldHelper.OpenChapterPanel(player,
                        exitFromGym ? CollabModule.Instance.Session.GymExitMapSID : map,
                        exitFromGym ? ReturnToLobbyMode.DoNotChangeReturn : returnToLobbyMode,
                        exitFromGym ? CollabModule.Instance.Session.GymExitSaveAllowed : savingAllowed,
                        exitFromGym);
                }
            ) { PlayerMustBeFacing = false });
        }

        public override void Update() {
            base.Update();
            talkComponent.Enabled = !InGameOverworldHelper.IsOpen;
        }
    }
}
