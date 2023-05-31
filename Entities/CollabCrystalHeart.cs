using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using System;

namespace Celeste.Mod.CollabUtils2.Entities {
    /// <summary>
    /// A Crystal Heart that functions like a vanilla one, but:
    /// - displays the in-level endscreen like mini hearts
    /// - return to lobby instead of returning to map
    /// </summary>
    [CustomEntity("CollabUtils2/CollabCrystalHeart")]
    public class CollabCrystalHeart : HeartGem {
        private static bool hooked = false;

        public CollabCrystalHeart(EntityData data, Vector2 offset) : base(data, offset) {
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            hook();
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            unhook();
        }

        public override void SceneEnd(Scene scene) {
            base.SceneEnd(scene);
            unhook();
        }

        private static void hook() {
            if (!hooked) {
                Logger.Log(LogLevel.Debug, "CollabUtils2/CollabCrystalHeart", "Hooking level complete methods");

                On.Celeste.Level.RegisterAreaComplete += onRegisterAreaComplete;
                IL.Celeste.Level.CompleteArea_bool_bool_bool += onCompleteArea;

                hooked = true;
            }
        }

        private static void unhook() {
            if (hooked) {
                Logger.Log(LogLevel.Debug, "CollabUtils2/CollabCrystalHeart", "Unhooking level complete methods");

                On.Celeste.Level.RegisterAreaComplete -= onRegisterAreaComplete;
                IL.Celeste.Level.CompleteArea_bool_bool_bool -= onCompleteArea;

                hooked = false;
            }
        }

        private static void onRegisterAreaComplete(On.Celeste.Level.orig_RegisterAreaComplete orig, Level self) {
            orig(self);

            // when the level is over and the poem pops up on-screen, the area complete info should also pop up if enabled.
            if (Settings.Instance.SpeedrunClock > SpeedrunType.Off && self.Tracker.CountEntities<AreaCompleteInfoInLevel>() == 0) {
                self.Add(new AreaCompleteInfoInLevel());
            }
        }

        private static void onCompleteArea(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(instr => instr.MatchCallvirt<Action>("Invoke"))) {
                Logger.Log("CollabUtils2/CollabCrystalHeart", $"Replacing action at {cursor.Index} in IL for Level.CompleteArea");

                cursor.Emit(OpCodes.Ldarg_0);

                // this is a bit confusing, but this function is returning a function that the vanilla code will run
                // (instead of the vanilla function that goes Engine.Scene = new LevelExit(...))
                cursor.EmitDelegate<Func<Action, Level, Action>>((orig, self) => () => {
                    Engine.Scene = new LevelExitToLobby(LevelExit.Mode.Completed, self.Session);
                });

                cursor.Index++;
            }
        }
    }
}
