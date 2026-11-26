using HarmonyLib;
using Verse;

namespace ResourceReadoutPlus;

// Starts each top-left message to the right of the readout panel; patched per message so it also works when a mod (Colony Groups) replaces Messages.MessagesDoGUI.
[HarmonyPatch(typeof(Message), nameof(Message.Draw))]
public static class Patch_Message_Draw
{
    public static void Prefix(ref int xOffset)
    {
        xOffset = ReadoutPanel.MessagesX(xOffset);
    }
}
