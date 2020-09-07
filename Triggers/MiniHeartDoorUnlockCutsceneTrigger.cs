using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger")]
    class MiniHeartDoorUnlockCutsceneTrigger : Trigger {
        public MiniHeartDoorUnlockCutsceneTrigger(EntityData data, Vector2 offset) : base(data, offset) { }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            MiniHeartDoor door = Scene.Entities.OfType<MiniHeartDoor>().FirstOrDefault();
            if (door != null && !door.Opened && door.Requires <= door.HeartGems && player != null) {
                // we got all hearts! trigger the cutscene.
                Scene.Add(new MiniHeartDoorUnlockCutscene(door, player));
                // and save this progress.
                CollabModule.Instance.SaveData.OpenedMiniHeartDoors.Add((Scene as Level).Session.Area.GetSID());
            }

            // this trigger is one-use.
            RemoveSelf();
        }
    }
}
