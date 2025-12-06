using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

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

        internal static void Load() {
            IL.Celeste.Strawberry.Added += modStrawberrySprite;
            collectRoutineHook = new ILHook(
                typeof(Strawberry).GetMethod("CollectRoutine", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget(),
                modStrawberrySound);
            playerDeathRoutineHook = new ILHook(
                typeof(PlayerDeadBody).GetMethod("DeathRoutine", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget(),
                modDeathSound);
            Everest.Events.Level.OnCreatePauseMenuButtons += onCreatePauseMenuButtons;
            On.Celeste.Player.Added += Player_Added;
            On.Celeste.SaveData.AddStrawberry_AreaKey_EntityID_bool += onSaveDataAddStrawberry;
            On.Celeste.Strawberry.CollectRoutine += onStrawberryCollectRoutine;
            On.Celeste.Level.End += onLevelEnd;

            // Any other mod blocking calls to Die to make Madeline invincible (like shadow dashes) should be able to also block the call to that hook on Die.
            // Otherwise, speed berries turn not golden and collect when Madeline is on the ground. This is bad.
            using (new DetourConfigContext(new DetourConfig("CollabUtils2_BeforeAll").WithPriority(int.MinValue)).Use()) {
                On.Celeste.Player.Die += onPlayerDie;
            }
        }

        internal static void Unload() {
            IL.Celeste.Strawberry.Added -= modStrawberrySprite;
            collectRoutineHook?.Dispose();
            playerDeathRoutineHook?.Dispose();
            Everest.Events.Level.OnCreatePauseMenuButtons -= onCreatePauseMenuButtons;
            On.Celeste.Player.Added -= Player_Added;
            On.Celeste.SaveData.AddStrawberry_AreaKey_EntityID_bool -= onSaveDataAddStrawberry;
            On.Celeste.Strawberry.CollectRoutine -= onStrawberryCollectRoutine;
            On.Celeste.Level.End -= onLevelEnd;

            On.Celeste.Player.Die -= onPlayerDie;
        }

        private static void Player_Added(On.Celeste.Player.orig_Added orig, Player self, Scene scene) {
            orig(self, scene);
            if (storedSpeedBerry != null) {
                SpeedBerry berry;
                if (scene.Tracker.CountEntities<SpeedBerry>() == 0) {
                    // create a new SpeedBerry in the current room
                    EntityData newData = storedSpeedBerry.EntityData;
                    Vector2 lastPos = newData.Position;
                    newData.Position = self.Position + new Vector2(8, -16);
                    scene.Add(berry = new SpeedBerry(newData, Vector2.Zero, storedSpeedBerry.ID, restored: true));
                    newData.Position = lastPos;
                    berry.TimerDisplay = storedSpeedBerry.TimerDisplay;
                    berry.TimerDisplay.TrackedBerry = berry;
                    self.Leader.GainFollower(berry.Follower);
                    storedSpeedBerry.RemoveSelf();
                } else {
                    storedSpeedBerry.TimerDisplay?.RemoveSelf();
                }
                storedSpeedBerry = null;
            }
        }

        private static void onCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal) {
            // create the Restart Speed Berry option at the bottom of the menu
            SpeedBerry berry;
            if ((berry = level.Tracker.GetEntity<SpeedBerry>()) != null && berry.Follower.HasLeader && !minimal) {
                TextMenu.Button item = new TextMenu.Button(Dialog.Clean("collabutils2_restartspeedberry")) {
                    OnPressed = () => {
                        level.Paused = false;
                        level.PauseMainMenuOpen = false;
                        menu.RemoveSelf();
                        berry.TimeRanOut = true;
                    }
                };
                menu.Add(item);
            }
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
                cursor.EmitDelegate<Func<Sprite, Strawberry, Sprite>>(replaceStrawberrySprite);
                cursor.Emit(OpCodes.Stfld, strawberrySprite);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, strawberrySprite);
            }
        }

        private static Sprite replaceStrawberrySprite(Sprite orig, Strawberry self) {
            // this method determines the strawberry sprite. "orig" is the original sprite, "self" is the strawberry.
            if (self is SilverBerry) {
                if (SaveData.Instance.CheckStrawberry(self.ID)) {
                    return GFX.SpriteBank.Create("CollabUtils2_ghostSilverBerry");
                }
                return GFX.SpriteBank.Create("CollabUtils2_silverBerry");
            }
            if (self is RainbowBerry) {
                if (SaveData.Instance.CheckStrawberry(self.ID)) {
                    return GFX.SpriteBank.Create("CollabUtils2_ghostRainbowBerry");
                }
                return GFX.SpriteBank.Create("CollabUtils2_rainbowBerry");
            }
            if (self is SpeedBerry) {
                if (SaveData.Instance.CheckStrawberry(self.ID)) {
                    return GFX.SpriteBank.Create("CollabUtils2_ghostSpeedBerry");
                }
                return GFX.SpriteBank.Create("CollabUtils2_speedBerry");
            }
            return orig;
        }

        private static void modStrawberrySound(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            FieldReference refToThis = HookHelper.FindReferenceToThisInCoroutine(cursor);

            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("event:/game/general/strawberry_get"))) {
                Logger.Log("CollabUtils2/StrawberryHooks", $"Modding golden sound at {cursor.Index} in IL for Strawberry.CollectRoutine");

                // we want to replace the vanilla collect sound with the silver berry one if the current berry is a silver one.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, refToThis);
                cursor.EmitDelegate<Func<string, Strawberry, string>>(replaceStrawberryGetSound);
            }
        }

        private static string replaceStrawberryGetSound(string orig, Strawberry self) {
            if (self is SilverBerry) {
                return "event:/SC2020_silverBerry_get";
            }
            if (self is RainbowBerry) {
                return "event:/SC2020_rainbowBerry_get";
            }
            if (self is SpeedBerry) {
                return "event:/SC2020_timedBerry_get";
            }
            return orig;
        }

        /// <summary>
        /// Used temporarily after the player dies with a speed berry that didn't run out of time to respawn the berry in the next screen
        /// </summary>
        private static SpeedBerry storedSpeedBerry;

        private static PlayerDeadBody onPlayerDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats) {
            bool hasSilver = false;
            SpeedBerry speedBerry = null;

            // check if the player is actually going to die first.
            if (!self.Dead && (evenIfInvincible || !SaveData.Instance.Assists.Invincible) && self.StateMachine.State != Player.StReflectionFall) {
                hasSilver = self.Leader.Followers.Any(follower => follower.Entity is SilverBerry);
                Follower speedBerryFollower = self.Leader.Followers.Find(follower => follower.Entity is SpeedBerry);
                if (speedBerryFollower != null) {
                    speedBerry = (SpeedBerry) speedBerryFollower.Entity;
                    // Don't restart the player to the starting room if there's still time left on the speed berry
                    if (!speedBerry.TimeRanOut) {
                        DynData<Strawberry> data = new DynData<Strawberry>(speedBerry);
                        data["Golden"] = false;
                        // set the starting position to the spawn point
                        Level level = self.SceneAs<Level>();
                        data["start"] = level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top)) + new Vector2(8, -16);
                    }
                }
            }

            PlayerDeadBody body = orig(self, direction, evenIfInvincible, registerDeathInStats);

            if (body != null) {
                DynData<PlayerDeadBody> data = new DynData<PlayerDeadBody>(body);
                data["hasSilver"] = hasSilver;
                data["hasSpeedBerry"] = (speedBerry != null);
                storedSpeedBerry = speedBerry;
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
                cursor.EmitDelegate<Func<string, PlayerDeadBody, string>>(modGoldenDeathSound);
            }
        }

        private static string modGoldenDeathSound(string orig, PlayerDeadBody self) {
            DynData<PlayerDeadBody> data = new DynData<PlayerDeadBody>(self);
            bool hasSilver = data.Get<bool>("hasSilver");
            bool hasSpeedBerry = data.Get<bool>("hasSpeedBerry");
            if (hasSilver && hasSpeedBerry) {
                return "event:/SC2020_silverTimedBerry_death";
            }
            if (hasSilver) {
                return "event:/SC2020_silverBerry_death";
            }
            if (hasSpeedBerry) {
                return "event:/SC2020_timedBerry_death";
            }
            return orig;
        }

        private static void onSaveDataAddStrawberry(On.Celeste.SaveData.orig_AddStrawberry_AreaKey_EntityID_bool orig,
            SaveData self, AreaKey area, EntityID strawberry, bool golden) {

            if (CollabMapDataProcessor.SpeedBerries.ContainsKey(area.GetSID())) {
                EntityID speedBerryID = CollabMapDataProcessor.SpeedBerries[area.GetSID()].ID;
                if (speedBerryID.Level == strawberry.Level && speedBerryID.ID == strawberry.ID) {
                    // this is the speed berry! abort
                    return;
                }
            }

            orig(self, area, strawberry, golden);
        }

        private static IEnumerator onStrawberryCollectRoutine(On.Celeste.Strawberry.orig_CollectRoutine orig, Strawberry self, int collectIndex) {
            Scene scene = self.Scene;

            IEnumerator origEnum = orig(self, collectIndex);
            while (origEnum.MoveNext()) {
                yield return origEnum.Current;
            }

            if (self is RainbowBerry) {
                // remove the strawberry points
                StrawberryPoints points = scene.Entities.ToAdd.OfType<StrawberryPoints>().FirstOrDefault();
                if (points != null) scene.Entities.ToAdd.Remove(points);

                // spawn a perfect effect instead
                scene.Add(new RainbowBerryPerfectEffect(self.Position));
            }
        }

        private static void onLevelEnd(On.Celeste.Level.orig_End orig, Level self) {
            orig(self);

            // we're leaving the level: forget about the stored speed berry.
            storedSpeedBerry = null;
        }
    }
}
