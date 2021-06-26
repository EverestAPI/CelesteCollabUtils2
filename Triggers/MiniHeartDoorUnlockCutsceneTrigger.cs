using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger")]
    [Tracked]
    class MiniHeartDoorUnlockCutsceneTrigger : Trigger {

        public static void Load() {
            On.Celeste.Level.AssistMode += modAssistModeOptions;
        }

        public static void Unload() {
            On.Celeste.Level.AssistMode -= modAssistModeOptions;
        }

        private static void modAssistModeOptions(On.Celeste.Level.orig_AssistMode orig, Level self, int returnIndex, bool minimal) {
            orig(self, returnIndex, minimal);

            // check if the player is inside an unlock cutscene trigger.
            Player player = self.Tracker.GetEntity<Player>();
            MiniHeartDoorUnlockCutsceneTrigger trigger = player?.CollideFirst<MiniHeartDoorUnlockCutsceneTrigger>();

            if (trigger != null) {
                MiniHeartDoor gate = trigger.findHeartGate();
                TextMenu menu = self.Entities.GetToAdd().OfType<TextMenu>().FirstOrDefault();

                // check that we have what we need, and that the gate can't open yet.
                if (menu != null && gate != null && !gate.Opened && gate.HeartGems < gate.Requires) {
                    // add an assist option to allow opening the door.
                    menu.Add(new TextMenu.Button(Dialog.Clean("collabutils2_assist_skip")) { ConfirmSfx = "event:/ui/main/message_confirm" }.Pressed(() => {
                        // show a confirm dialog, and focus this one instead of the assist menu.
                        menu.Focused = false;
                        self.Add(new AssistSkipConfirmUI(
                            onConfirm: () => {
                                // open the gate!
                                gate.ForceAllHearts = true;
                                trigger.openHeartGate(player);

                                // usual "close pause menu" stuff (minus the sound).
                                self.Paused = false;
                                new DynData<Level>(self)["unpauseTimer"] = 0.15f;
                                menu.Close();
                            },

                            onCancel: () => {
                                // give focus back to the menu.
                                menu.Focused = true;
                            }));
                    }));
                }
            }
        }


        private string doorID;

        public MiniHeartDoorUnlockCutsceneTrigger(EntityData data, Vector2 offset) : base(data, offset) {
            doorID = data.Attr("doorID");
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            MiniHeartDoor gate = findHeartGate();
            if (gate == null || gate.Opened) {
                // the door was already opened or doesn't exist so we have no purpose here!
                RemoveSelf();
            }
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            openHeartGate(player);
        }

        private MiniHeartDoor findHeartGate() {
            // find the heart gate that matches the ID of the trigger.
            return Scene.Entities.OfType<MiniHeartDoor>().Where(d => d.DoorID == doorID).FirstOrDefault();
        }

        // if "force" is true, the cutscene will trigger even if the player doesn't have enough hearts.
        private void openHeartGate(Player player) {
            MiniHeartDoor door = findHeartGate();
            if (door != null && !door.Opened && door.Requires <= door.HeartGems && player != null) {
                // we got all hearts! trigger the cutscene.
                Scene.Add(new MiniHeartDoorUnlockCutscene(door, player));
                // and save this progress.
                CollabModule.Instance.SaveData.OpenedMiniHeartDoors.Add(door.GetDoorSaveDataID(Scene as Level));

                // remove this trigger once that it was used.
                RemoveSelf();
            }
        }
    }
}
