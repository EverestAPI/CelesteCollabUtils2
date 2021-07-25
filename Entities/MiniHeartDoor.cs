using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/MiniHeartDoor")]
    public class MiniHeartDoor : HeartGemDoor {
        private static Hook hookOnHeartCount;
        private static ILHook hookOnDoorRoutine;

        private static readonly Dictionary<string, string> colors = new Dictionary<string, string>() {
            { "beginner", "18668F" },
            { "intermediate", "E0233D" },
            { "advanced", "896900" },
            { "expert", "824207" },
            { "grandmaster", "650091" }
        };

        internal static void Load() {
            hookOnHeartCount = new Hook(typeof(HeartGemDoor).GetMethod("get_HeartGems"),
                typeof(MiniHeartDoor).GetMethod("getCollectedHeartGems", BindingFlags.NonPublic | BindingFlags.Static));
            hookOnDoorRoutine = HookHelper.HookCoroutine("Celeste.HeartGemDoor", "Routine", modDoorRoutine);
            IL.Celeste.HeartGemDoor.DrawInterior += modDoorColor;
        }

        internal static void Unload() {
            hookOnHeartCount?.Dispose();
            hookOnDoorRoutine?.Dispose();
            IL.Celeste.HeartGemDoor.DrawInterior -= modDoorColor;
        }

        private delegate int orig_get_HeartGems(HeartGemDoor self);

        private static int getCollectedHeartGems(orig_get_HeartGems orig, HeartGemDoor self) {
            if (self is MiniHeartDoor selfMiniHeartDoor) {
                if (selfMiniHeartDoor.ForceAllHearts) {
                    // door was told to pretend all hearts were collected, so just do that
                    return self.Requires;
                }

                // otherwise, check how many hearts we have for the door's assigned level set
                return SaveData.Instance.GetLevelSetStatsFor(selfMiniHeartDoor.levelSet).TotalHeartGems;
            }

            return orig(self);
        }


        private static void modDoorRoutine(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            FieldReference refToThis = HookHelper.FindReferenceToThisInCoroutine(cursor);

            // find the "player is on the left side of the door" check
            // FNA version
            bool hookPointFound = cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdarg(0),
                instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name.Contains("player"),
                instr => instr.MatchCallvirt<Entity>("get_X"),
                instr => instr.MatchLdarg(0),
                instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name == "<>4__this",
                instr => instr.MatchCallvirt<Entity>("get_X"));

            if (!hookPointFound) {
                cursor.Index = 0;

                // XNA version
                hookPointFound = cursor.TryGotoNext(MoveType.After,
                    instr => instr.OpCode == OpCodes.Ldloc_S,
                    instr => instr.MatchCallvirt<Entity>("get_X"),
                    instr => instr.OpCode == OpCodes.Ldloc_1,
                    instr => instr.MatchCallvirt<Entity>("get_X"));
            }

            if (hookPointFound) {
                Logger.Log("CollabUtils2/MiniHeartDoor", $"Making mini heart door approachable from both sides at {cursor.Index} in IL for HeartGemDoor.Routine");
                cursor.Index--;
                cursor.Emit(OpCodes.Dup);
                cursor.Index++;
                cursor.EmitDelegate<Func<HeartGemDoor, float, float>>((self, orig) => {
                    if (self is MiniHeartDoor door) {
                        // actually check the Y poition of the player instead.
                        Player player = self.Scene.Tracker.GetEntity<Player>();
                        if (door.ForceTrigger || (player != null && player.Center.Y > door.Y - door.height && player.Center.Y < door.Y + door.height)) {
                            // player has same height as door => ok (return MaxValue so that the door is further right than the player)
                            return float.MaxValue;
                        } else {
                            // player has not same height as door => ko (return MinValue so that the door is further left than the player)
                            return float.MinValue;
                        }
                    }
                    return orig;
                });
            }

            cursor.Index = 0;

            if (refToThis != null && cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(80f))) {
                Logger.Log("CollabUtils2/MiniHeartDoor", $"Making mini heart door open on command at {cursor.Index} in IL for HeartGemDoor.Routine");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, refToThis);
                cursor.EmitDelegate<Func<float, HeartGemDoor, float>>((orig, self) => {
                    if (self is MiniHeartDoor door && door.ForceTrigger) {
                        // force trigger the door by replacing the approach distance by... basically positive infinity.
                        return float.MaxValue;
                    }
                    return orig;
                });
            }

            // add a patch at the place where the vanilla opened_heartgem_door_[heartcount] gets set.
            if (refToThis != null && cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("opened_heartgem_door_"))) {
                Logger.Log("CollabUtils2/MiniHeartDoor", $"Making mini heart door save its session flag at {cursor.Index} in IL for HeartGemDoor.Routine");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, refToThis);
                cursor.EmitDelegate<Action<HeartGemDoor>>(self => {
                    if (self is MiniHeartDoor door) {
                        // mini heart doors use their own flags, that allow better differenciation between themselves by using entity ID.
                        (door.Scene as Level).Session.SetFlag("opened_mini_heart_door_" + door.entityID, true);
                    }
                });
            }
        }

        private static void modDoorColor(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("18668f"))) {
                Logger.Log("CollabUtils2/MiniHeartDoor", $"Modding door at {cursor.Index} in IL for HeartGemDoor.DrawInterior");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<string, HeartGemDoor, string>>((orig, self) => {
                    if (self is MiniHeartDoor miniHeartDoor) {
                        return miniHeartDoor.color;
                    }
                    return orig;
                });
            }
        }

        public Solid TopSolid;
        public Solid BottomSolid;

        private string levelSet;
        private string color;
        public bool ForceTrigger = false; // pretend the player is close to the door
        public bool ForceAllHearts = false; // pretend the player has all required hearts

        private float height;

        private EntityID entityID;
        public string DoorID;
        public string GetDoorSaveDataID(Scene scene) {
            return (scene as Level).Session.Area.GetSID() + (string.IsNullOrEmpty(DoorID) ? "" : ":" + DoorID);
        }

        public MiniHeartDoor(EntityData data, Vector2 offset, EntityID entityID) : base(data, offset) {
            height = data.Height;
            levelSet = data.Attr("levelSet");

            color = data.Attr("color");
            if (colors.ContainsKey(color)) {
                color = colors[color];
            }

            this.entityID = entityID;
            DoorID = data.Attr("doorID");
        }

        public override void Added(Scene scene) {
            // if the gate was already opened on that save or in that session, open the door right away by setting the flag.
            (scene as Level).Session.SetFlag("opened_heartgem_door_" + Requires,
                (scene as Level).Session.GetFlag("opened_mini_heart_door_" + entityID) || CollabModule.Instance.SaveData.OpenedMiniHeartDoors.Contains(GetDoorSaveDataID(scene)));

            base.Added(scene);

            DynData<HeartGemDoor> self = new DynData<HeartGemDoor>(this);
            TopSolid = self.Get<Solid>("TopSolid");
            BottomSolid = self.Get<Solid>("BotSolid");

            // resize the gate: it shouldn't take the whole screen height.
            TopSolid.Collider.Height = height;
            BottomSolid.Collider.Height = height;
            TopSolid.Top = Y - height;
            BottomSolid.Bottom = Y + height;

            if (Opened) {
                // place the blocks correctly in an open position.
                float openDistance = self.Get<float>("openDistance");
                TopSolid.Collider.Height -= openDistance;
                BottomSolid.Collider.Height -= openDistance;
                BottomSolid.Top += openDistance;
            }
        }

        public override void Update() {
            base.Update();

            // make sure the two blocks don't escape their boundaries when the door opens up.
            if (TopSolid.Top != Y - height) {
                // if the top block is at 12 instead of 16, and has height 30, make it height 26 instead.
                float displacement = (Y - height) - TopSolid.Top; // 20 - 16 = 4
                TopSolid.Collider.Height = height - displacement; // 30 - 4 = 26
                TopSolid.Top = Y - height; // replace the block at 16
            }
            if (BottomSolid.Bottom != Y + height) {
                float displacement = BottomSolid.Top - Y;
                BottomSolid.Collider.Height = height - displacement;
            }
        }
    }
}
