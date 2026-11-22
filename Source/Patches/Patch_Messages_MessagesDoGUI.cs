using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

// Starts the top-left messages to the right of the readout panel instead of at the base game's fixed x, so a wide panel doesn't cover them.
[HarmonyPatch(typeof(Messages), nameof(Messages.MessagesDoGUI))]
public static class Patch_Messages_MessagesDoGUI
{
    private static readonly FieldInfo TopLeft = AccessTools.Field(typeof(Messages), nameof(Messages.MessagesTopLeftStandard));
    private static readonly FieldInfo X = AccessTools.Field(typeof(Vector2), nameof(Vector2.x));

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        int load = codes.FindIndex(code => code.LoadsField(TopLeft) || code.LoadsField(TopLeft, byAddress: true));
        int x = load < 0 ? -1 : codes.FindIndex(load, code => code.LoadsField(X));
        if (x < 0)
        {
            Log.Warning("[Resource Readout+] Couldn't find where messages are placed; they keep the base game's position.");
            return codes;
        }

        codes.Insert(x + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ReadoutPanel), nameof(ReadoutPanel.MessagesX))));
        return codes;
    }
}
