using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger")]
    class MiniHeartDoorUnlockCutsceneTrigger : Trigger {
        private string doorID;

        public MiniHeartDoorUnlockCutsceneTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            doorID = data.Attr("doorID");
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            MiniHeartDoor door = Scene.Entities.OfType<MiniHeartDoor>().Where(d => d.DoorID == doorID).FirstOrDefault();
            if (door != null && !door.Opened && door.Requires <= door.HeartGems && player != null) {
                // we got all hearts! trigger the cutscene.
                Scene.Add(new MiniHeartDoorUnlockCutscene(door, player));
                // and save this progress.
                CollabModule.Instance.SaveData.OpenedMiniHeartDoors.Add(door.GetDoorSaveDataID(Scene as Level));
            }

            // this trigger is one-use.
            RemoveSelf();
        }
    }
}
