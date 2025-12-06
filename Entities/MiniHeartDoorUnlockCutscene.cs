using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.Entities {
    class MiniHeartDoorUnlockCutscene : CutsceneEntity {
        private MiniHeartDoor door;
        private Player player;

        public MiniHeartDoorUnlockCutscene(MiniHeartDoor door, Player player) {
            this.door = door;
            this.player = player;
        }

        public override void OnBegin(Level level) {
            Add(new Coroutine(Cutscene(level)));
        }

        private IEnumerator Cutscene(Level level) {
            // wait for the respawn animation to be over.
            while (player.StateMachine.State != Player.StNormal) {
                yield return null;
            }

            player.StateMachine.State = Player.StDummy;

            yield return 0.5f;

            // pan the camera to the door.
            yield return CameraTo(door.Center - new Vector2(160f - door.Size / 2, 90f), 1f, Ease.CubeOut);

            // make door open.
            door.ForceTrigger = true;

            // wait for door to be open.
            while (door.openPercent < 1f) {
                yield return null;
            }
            yield return 1f;

            // pan back to player.
            yield return CameraTo(player.CameraTarget, 1f, Ease.CubeOut);

            // cutscene over.
            EndCutscene(level);
        }

        public override void OnEnd(Level level) {
            player.StateMachine.State = Player.StNormal;
            player.ForceCameraUpdate = false;

            if (WasSkipped) {
                // snap camera to player
                level.Camera.Position = player.CameraTarget;

                // instant open the door
                door.openPercent = 1f;
                door.Opened = true;
                door.Counter = door.Requires;
                float openDistance = door.openDistance;
                door.TopSolid.Bottom = door.Y - openDistance;
                door.BotSolid.Top = door.Y + openDistance;

                // and throw the open animation out of the window.
                foreach (Component component in door) {
                    if (component is Coroutine) {
                        component.RemoveSelf();
                        break;
                    }
                }
            }
        }
    }
}
