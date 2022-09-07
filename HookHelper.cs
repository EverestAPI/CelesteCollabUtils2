using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using MonoMod.Utils;
using Mono.Cecil.Cil;

namespace Celeste.Mod.CollabUtils2 {
    public static class HookHelper {
        /// <summary>
        /// Utility method to patch "coroutine" kinds of methods with IL.
        /// Those methods' code reside in a compiler-generated method, and IL.Celeste.* do not allow manipulating them directly.
        /// </summary>
        /// <param name="manipulator">Method taking care of the patching</param>
        /// <returns>The IL hook if the actual code was found, null otherwise</returns>
        public static ILHook HookCoroutine(string typeName, string methodName, ILContext.Manipulator manipulator) {
            // get the Celeste.exe module definition Everest loaded for us
            ModuleDefinition celeste = Everest.Relinker.SharedRelinkModuleMap["Celeste.Mod.mm"];

            // get the type
            TypeDefinition type = celeste.GetType(typeName);
            if (type == null)
                return null;

            // the "coroutine" method is actually a nested type tracking the coroutine's state
            // (to make it restart from where it stopped when MoveNext() is called).
            // what we see in ILSpy and what we want to hook is actually the MoveNext() method in this nested type.
            foreach (TypeDefinition nest in type.NestedTypes) {
                if (nest.Name.StartsWith("<" + methodName + ">d__")) {
                    // check that this nested type contains a MoveNext() method
                    MethodDefinition method = nest.FindMethod("System.Boolean MoveNext()");
                    if (method == null)
                        return null;

                    // we found it! let's convert it into basic System.Reflection stuff and hook it.
                    Logger.Log("CollabUtils2/HookHelper", $"Building IL hook for method {method.FullName} in order to mod {typeName}.{methodName}()");
                    Type reflectionType = typeof(Player).Assembly.GetType(typeName);
                    Type reflectionNestedType = reflectionType.GetNestedType(nest.Name, BindingFlags.NonPublic);
                    MethodBase moveNextMethod = reflectionNestedType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
                    return new ILHook(moveNextMethod, manipulator);
                }
            }

            return null;
        }

        public static FieldReference FindReferenceToThisInCoroutine(ILCursor cursor) {
            // coroutines are cursed and references to "this" are actually references to this.<>4__this
            cursor.GotoNext(instr => instr.OpCode == OpCodes.Ldfld && (instr.Operand as FieldReference).Name == "<>4__this");
            FieldReference refToThis = cursor.Next.Operand as FieldReference;
            cursor.Index = 0;
            return refToThis;
        }
    }
}
