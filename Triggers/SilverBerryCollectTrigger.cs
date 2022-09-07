using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/SilverBerryCollectTrigger")]
    [Tracked]
    class SilverBerryCollectTrigger : Trigger {
        private static ILHook strawberryUpdateHook = null;

        public static void Load() {
            strawberryUpdateHook = new ILHook(typeof(Strawberry).GetMethod("orig_Update"), hookStrawberryUpdate);
        }

        public static void Unload() {
            strawberryUpdateHook?.Dispose();
            strawberryUpdateHook = null;
        }

        public SilverBerryCollectTrigger(EntityData data, Vector2 offset) : base(data, offset) { }

        private static void hookStrawberryUpdate(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(
                instr => instr.OpCode == OpCodes.Callvirt && (instr.Operand as MethodReference)?.FullName == "System.Boolean Monocle.Entity::CollideCheck<Celeste.GoldBerryCollectTrigger>()")) {

                Logger.Log("CollabUtils2/SilverBerryCollectTrigger", $"Modding gold berry collect at {cursor.Index} in IL for Strawberry.orig_Update");

                // argument 1 (player) is the same player that is used for the vanilla call, so clone it
                cursor.Emit(OpCodes.Dup);

                // argument 2 (orig) will be the result of the vanilla call to CollideCheck<GoldBerryCollectTrigger>()
                cursor.Index++;

                // argument 3 (self) is the strawberry that is being checked: the method we are hooking is part of it
                cursor.Emit(OpCodes.Ldarg_0);

                cursor.EmitDelegate<Func<Player, bool, Strawberry, bool>>((player, orig, self) => {
                    // collect golden berry and derivatives if the player is in a golden berry trigger, OR if it is a silver berry and the player is in a silver berry collect trigger.
                    return orig || ((self is SilverBerry) && player.CollideCheck<SilverBerryCollectTrigger>());
                });
            }
        }
    }
}
