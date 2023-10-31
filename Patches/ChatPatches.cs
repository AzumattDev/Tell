using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Tell.Patches;

[HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), typeof(string), typeof(string), typeof(Talker.Type), typeof(bool))]
public static class Terminal_AddString_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        return NoMoreForcedCase(code);
    }

    public static IEnumerable<CodeInstruction> NoMoreForcedCase(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codeInstructionList = new List<CodeInstruction>(instructions);
        for (int index = 0; index < codeInstructionList.Count; ++index)
        {
            if (codeInstructionList[index].opcode == OpCodes.Callvirt)
            {
                MethodBase? operand = codeInstructionList[index].operand as MethodBase;
                if (operand != null && operand.Name is "ToLowerInvariant" or "ToUpper")
                {
                    codeInstructionList.RemoveRange(index - 1, 3);
                    index -= 2;
                }
            }
        }

        return codeInstructionList;
    }
}

[HarmonyPatch(typeof(Chat), nameof(Chat.AddInworldText))]
public static class Chat_MixedCase_Chat_Patch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        return Terminal_AddString_Patch.NoMoreForcedCase(code);
    }
}