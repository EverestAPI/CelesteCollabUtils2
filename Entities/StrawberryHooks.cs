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

            // catch the moment where the sprite is added to the entity
            if (cursor.TryGotoNext(
                instr => instr.MatchLdarg(0),
                instr => instr.MatchLdfld<Strawberry>("sprite"),
                instr => instr.MatchCall<Entity>("Add"))) {

                cursor.Index++;

                FieldReference strawberrySprite = cursor.Next.Operand as FieldReference;

                // mod the sprite
                Logger.Log("CollabUtils2/StrawberryHooks", $"Modding strawberry sprite at {cursor.Index} in IL for Strawberry.Added");

                cursor.Emit(OpCodes.Ldarg_0); // for stfld
                cursor.Index++;
                cursor.Emit(OpCodes.Ldarg_0); // for the delegate call
                cursor.EmitDelegate<Func<Sprite, Strawberry, Sprite>>((orig, self) => {
                    // this method determines the strawberry sprite. "orig" is the original sprite, "self" is the strawberry.
                    if (self is SilverBerry) {
                        if (SaveData.Instance.CheckStrawberry(self.ID)) {
                            return SilverBerry.SpriteBank.Create("ghostSilverBerry");
                        }
                        return SilverBerry.SpriteBank.Create("silverBerry");
                    }
                    if (self is RainbowBerry) {
                        if (SaveData.Instance.CheckStrawberry(self.ID)) {
                            return RainbowBerry.SpriteBank.Create("ghostRainbowBerry");
                        }
                        return RainbowBerry.SpriteBank.Create("rainbowBerry");
                    }
                    return orig;
                });
                cursor.Emit(OpCodes.Stfld, strawberrySprite);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, strawberrySprite);
            }
        }

        private static void modStrawberrySound(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            FieldReference refToThis = HookHelper.FindReferenceToThisInCoroutine(cursor);

            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("event:/game/general/strawberry_get"))) {
                Logger.Log("CollabUtils2/StrawberryHooks", $"Modding golden sound at {cursor.Index} in IL for Strawberry.CollectRoutine");

                // we want to replace the vanilla collect sound with the silver berry one if the current berry is a silver one.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, refToThis);
                cursor.EmitDelegate<Func<string, Strawberry, string>>((orig, self) => {
                    if (self is SilverBerry) {
                        return "event:/SC2020_silverBerry_get";
                    }
                    if (self is RainbowBerry) {
                        return "event:/SC2020_rainbowBerry_get";
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
            FieldReference refToThis = HookHelper.FindReferenceToThisInCoroutine(cursor);

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
    }
}
