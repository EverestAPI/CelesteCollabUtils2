using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    /// <summary>
    /// This class sets up some hooks that will be useful for silver berries, speed berries and rainbow berries.
    /// They mod the following things:
    /// - strawberry sprite for silvers and rainbows
    /// - death sounds for silvers, speeds, and both at the same time
    /// - collect sounds for silvers and rainbows
    /// </summary>
    static class StrawberryHooks {
        private static ILHook collectRoutineHook;
        private static ILHook playerDeathRoutineHook;

        public static void Load() {
            IL.Celeste.Strawberry.Added += modStrawberrySprite;
            collectRoutineHook = HookHelper.HookCoroutine("Celeste.Strawberry", "CollectRoutine", modStrawberrySound);
            On.Celeste.Player.Die += onPlayerDie;
            playerDeathRoutineHook = HookHelper.HookCoroutine("Celeste.PlayerDeadBody", "DeathRoutine", modDeathSound);
        }

        public static void Unload() {
            IL.Celeste.Strawberry.Added -= modStrawberrySprite;
            collectRoutineHook?.Dispose();
            On.Celeste.Player.Die -= onPlayerDie;
            playerDeathRoutineHook?.Dispose();
        }

        private static void modStrawberrySprite(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // catch GFX.SpriteBank.Create("goldberry")
            if (cursor.TryGotoNext(
                instr => instr.MatchLdsfld(typeof(GFX), "SpriteBank"),
                instr => instr.MatchLdstr("goldberry"),
                instr => instr.MatchCallvirt<SpriteBank>("Create"))) {

                cursor.Index++;
                Logger.Log("CollabUtils2/StrawberryHooks", $"Modding golden sprite at {cursor.Index} in IL for Strawberry.Added");

                // we want to replace the vanilla sprite bank with the silver berry bank if the current berry is a silver one.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<SpriteBank, Strawberry, SpriteBank>>((orig, self) => {
                    if (self is SilverBerry) {
                        return SilverBerry.SpriteBank;
                    }
                    return orig;
                });
            }
        }

        private static void modStrawberrySound(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            FieldReference refToThis = findReferenceToThisInCoroutine(cursor);

            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("event:/game/general/strawberry_get"))) {
                Logger.Log("CollabUtils2/StrawberryHooks", $"Modding golden sound at {cursor.Index} in IL for Strawberry.CollectRoutine");

                // we want to replace the vanilla collect sound with the silver berry one if the current berry is a silver one.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, refToThis);
                cursor.EmitDelegate<Func<string, Strawberry, string>>((orig, self) => {
                    if (self is SilverBerry) {
                        return "event:/SC2020_silverBerry_get";
                    }
                    return orig;
                });
            }
        }

        private static PlayerDeadBody onPlayerDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats) {
            bool hasSilver = self.Leader.Followers.Any(follower => follower.Entity is SilverBerry);

            PlayerDeadBody body = orig(self, direction, evenIfInvincible, registerDeathInStats);

            if (body != null) {
                new DynData<PlayerDeadBody>(body)["hasSilver"] = hasSilver;
            }
            return body;
        }

        private static void modDeathSound(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            FieldReference refToThis = findReferenceToThisInCoroutine(cursor);

            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("event:/new_content/char/madeline/death_golden"))) {
                Logger.Log("CollabUtils2/StrawberryHooks", $"Modding golden death sound at {cursor.Index} in IL for PlayerDeadBody.DeathRoutine");

                // we want to replace the vanilla death sound with the silver berry one if carrying a silver berry.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, refToThis);
                cursor.EmitDelegate<Func<string, PlayerDeadBody, string>>((orig, self) => {
                    if (new DynData<PlayerDeadBody>(self).Get<bool>("hasSilver")) {
                        return "event:/SC2020_silverBerry_death";
                    }
                    return orig;
                });
            }
        }

        private static FieldReference findReferenceToThisInCoroutine(ILCursor cursor) {
            // coroutines are cursed and references to "this" are actually references to this.<>4__this
            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name == "<>4__this");
            FieldReference refToThis = cursor.Next.Operand as FieldReference;
            cursor.Index = 0;
            return refToThis;
        }
    }
}
