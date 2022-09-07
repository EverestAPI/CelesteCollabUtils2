using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using System;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/SilverBlock")]
    public class SilverBlock : GoldenBlock {
        internal static void Load() {
            IL.Celeste.GoldenBlock.ctor_Vector2_float_float += modGoldenBlockConstructor;
        }
        internal static void Unload() {
            IL.Celeste.GoldenBlock.ctor_Vector2_float_float -= modGoldenBlockConstructor;
        }

        private static void modGoldenBlockConstructor(ILContext il) {
            // replace golden textures with silver ones for silver blocks.
            ILCursor cursor = new ILCursor(il);
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("collectables/goldberry/idle00"))) {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<string, GoldenBlock, string>>((orig, self) => self is SilverBlock ? "CollabUtils2/silverBerry/idle00" : orig);
            }
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr("objects/goldblock"))) {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<string, GoldenBlock, string>>((orig, self) => self is SilverBlock ? "CollabUtils2/silverblock" : orig);
            }
        }

        public SilverBlock(EntityData data, Vector2 offset) : base(data, offset) { }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            // remove the block if the player doesn't have a silver berry.
            bool hasSilverBerry = false;
            foreach (Strawberry item in scene.Entities.FindAll<Strawberry>()) {
                if (item is SilverBerry && item.Follower.Leader != null) {
                    hasSilverBerry = true;
                    break;
                }
            }
            if (!hasSilverBerry) {
                RemoveSelf();
            }
        }
    }
}
