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
    class MiniHeartDoor : HeartGemDoor {
        private static Hook hookOnHeartCount;
        private static ILHook hookOnDoorRoutine;

        private static readonly Dictionary<string, string> colors = new Dictionary<string, string>() {
            { "beginner", "18668F" },
            { "intermediate", "E0233D" },
            { "advanced", "896900" },
            { "expert", "824207" },
            { "grandmaster", "650091" }
        };

        public static void Load() {
            hookOnHeartCount = new Hook(typeof(HeartGemDoor).GetMethod("get_HeartGems"),
                typeof(MiniHeartDoor).GetMethod("getCollectedHeartGems", BindingFlags.NonPublic | BindingFlags.Static));
            hookOnDoorRoutine = HookHelper.HookCoroutine("Celeste.HeartGemDoor", "Routine", modDoorRoutine);
            IL.Celeste.HeartGemDoor.DrawInterior += modDoorColor;
        }

        public static void Unload() {
            hookOnHeartCount?.Dispose();
            hookOnDoorRoutine?.Dispose();
            IL.Celeste.HeartGemDoor.DrawInterior -= modDoorColor;
        }

        private delegate int orig_get_HeartGems(HeartGemDoor self);

        private static int getCollectedHeartGems(orig_get_HeartGems orig, HeartGemDoor self) {
            if (!SaveData.Instance.CheatMode && self is MiniHeartDoor) {
                return SaveData.Instance.GetLevelSetStatsFor((self as MiniHeartDoor).levelSet).TotalHeartGems;
            }
            return orig(self);
        }


        private static void modDoorRoutine(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            FieldReference refToThis = null;

            // find the "player is on the left side of the door" check
            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdarg(0),
                instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name.Contains("player"),
                instr => instr.MatchCallvirt<Entity>("get_X"),
                instr => instr.MatchLdarg(0),
                instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name == "<>4__this",
                instr => instr.MatchCallvirt<Entity>("get_X"))) {

                Logger.Log("CollabUtils2/MiniHeartDoor", $"Making mini heart door approachable from both sides at {cursor.Index} in IL for HeartGemDoor.Routine");
                cursor.Index--;
                refToThis = (cursor.Prev.Operand as FieldReference);
                cursor.Emit(OpCodes.Dup);
                cursor.Index++;
                cursor.EmitDelegate<Func<HeartGemDoor, float, float>>((self, orig) => {
                    if (self is MiniHeartDoor door) {
                        // actually check the Y poition of the player instead.
                        Player player = self.Scene.Tracker.GetEntity<Player>();
                        if (player != null && player.Center.Y > door.Y - door.height && player.Center.Y < door.Y + door.height) {
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

        private Solid topSolid;
        private Solid bottomSolid;
        private string levelSet;
        private string color;
        public bool ForceTrigger = false;

        private float height;

        public MiniHeartDoor(EntityData data, Vector2 offset) : base(data, offset) {
            height = data.Height;
            levelSet = data.Attr("levelSet");

            color = data.Attr("color");
            if (colors.ContainsKey(color)) {
                color = colors[color];
            }
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            DynData<HeartGemDoor> self = new DynData<HeartGemDoor>(this);
            topSolid = self.Get<Solid>("TopSolid");
            bottomSolid = self.Get<Solid>("BotSolid");

            topSolid.Collider.Height = height;
            bottomSolid.Collider.Height = height;
            topSolid.Top = Y - height;
            bottomSolid.Bottom = Y + height;

            if (Opened) {
                float openDistance = self.Get<float>("openDistance");
                topSolid.Collider.Height -= openDistance;
                bottomSolid.Collider.Height -= openDistance;
                bottomSolid.Top += openDistance;
            }
        }

        public override void Update() {
            base.Update();

            // make sure the two blocks don't escape their boundaries when the door opens up.
            if (topSolid.Top != Y - height) {
                // if the top block is at 12 instead of 16, and has height 30, make it height 26 instead.
                float displacement = (Y - height) - topSolid.Top; // 20 - 16 = 4
                topSolid.Collider.Height = height - displacement; // 30 - 4 = 26
                topSolid.Top = Y - height; // replace the block at 16
            }
            if (bottomSolid.Bottom != Y + height) {
                float displacement = bottomSolid.Top - Y;
                bottomSolid.Collider.Height = height - displacement;
            }
        }
    }
}
