using Mono.Cecil;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace Celeste.Mod.CollabUtils2 {
    public static class HookHelper {
        public static FieldReference FindReferenceToThisInCoroutine(ILCursor cursor) {
            // coroutines are cursed and references to "this" are actually references to this.<>4__this
            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name == "<>4__this");
            FieldReference refToThis = cursor.Next.Operand as FieldReference;
            cursor.Index = 0;
            return refToThis;
        }
    }
}
