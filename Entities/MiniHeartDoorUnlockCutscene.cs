using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.Entities {
    class MiniHeartDoorUnlockCutscene : CutsceneEntity {
        private MiniHeartDoor door;
        private Player player;

        private Vector2 origCameraAnchor;
        private Vector2 origCameraAnchorLerp;

        public MiniHeartDoorUnlockCutscene(MiniHeartDoor door, Player player) {
            this.door = door;
            this.player = player;
        }

        public override void OnBegin(Level level) {
            origCameraAnchor = player.CameraAnchor;
            origCameraAnchorLerp = player.CameraAnchorLerp;

            Add(new Coroutine(Cutscene(level)));
        }

        private IEnumerator Cutscene(Level level) {
            // wait for the respawn animation to be over.
            while (player.StateMachine.State != 0) {
                yield return null;
            }

            player.StateMachine.State = 11;
            player.ForceCameraUpdate = true;

            yield return 0.5f;

            // kill camera targets before they mess with our plans.
            foreach (Trigger trigger in Scene.Tracker.GetEntities<CameraTargetTrigger>()) {
                trigger.Collidable = false;
            }
            foreach (Trigger trigger in Scene.Tracker.GetEntities<CameraAdvanceTargetTrigger>()) {
                trigger.Collidable = false;
            }
            yield return null;

            // the camera targets' OnLeave were called, now set our own target.
            player.CameraAnchor = door.Center - new Vector2(160f - door.Size / 2, 90f);
            player.CameraAnchorLerp = Vector2.One;

            // wait for the camera to reach its objective.
            Vector2 prevCameraPosition = level.Camera.Position;
            yield return null;
            while ((level.Camera.Position - prevCameraPosition).LengthSquared() > 0.01f) {
                prevCameraPosition = level.Camera.Position;
                yield return null;
            }

            // make door open.
            door.ForceTrigger = true;

            // wait for door to be open.
            DynData<HeartGemDoor> doorData = new DynData<HeartGemDoor>(door);
            while (doorData.Get<float>("openPercent") < 1f) {
                yield return null;
            }
            yield return 1f;

            // pan back to player.
            player.CameraAnchor = origCameraAnchor;
            player.CameraAnchorLerp = origCameraAnchorLerp;

            // revive camera targets.
            foreach (Trigger trigger in Scene.Tracker.GetEntities<CameraTargetTrigger>()) {
                trigger.Collidable = true;
            }
            foreach (Trigger trigger in Scene.Tracker.GetEntities<CameraAdvanceTargetTrigger>()) {
                trigger.Collidable = true;
            }

            // wait for the camera to reach its objective.
            prevCameraPosition = level.Camera.Position;
            yield return null;
            while ((level.Camera.Position - prevCameraPosition).LengthSquared() > 0.01f) {
                prevCameraPosition = level.Camera.Position;
                yield return null;
            }

            // cutscene over.
            EndCutscene(level);
        }

        public override void OnEnd(Level level) {
            player.StateMachine.State = 0;
            player.ForceCameraUpdate = false;

            if (WasSkipped) {
                // snap camera to player
                player.CameraAnchor = origCameraAnchor;
                player.CameraAnchorLerp = origCameraAnchorLerp;
                level.Camera.Position = player.CameraTarget;

                // instant open the door
                DynData<HeartGemDoor> doorData = new DynData<HeartGemDoor>(door);
                doorData["openPercent"] = 1f;
                doorData["Opened"] = true;
                doorData["Counter"] = door.Requires;
                float openDistance = doorData.Get<float>("openDistance");
                door.TopSolid.Bottom = door.Y - openDistance;
                door.BottomSolid.Top = door.Y + openDistance;

                // and throw the open animation out of the window.
                foreach (Component component in door) {
                    if (component is Coroutine) {
                        component.RemoveSelf();
                        break;
                    }
                }

                // be sure to revive camera targets.
                foreach (Trigger trigger in Scene.Tracker.GetEntities<CameraTargetTrigger>()) {
                    trigger.Collidable = true;
                }
                foreach (Trigger trigger in Scene.Tracker.GetEntities<CameraAdvanceTargetTrigger>()) {
                    trigger.Collidable = true;
                }
            }
        }
    }
}
