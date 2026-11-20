using HarmonyLib;
using RimWorld;

namespace ResourceReadoutPlus;

// Draws this mod's readout instead of the base game's; the base game's draws again only if ours failed this session.
[HarmonyPatch(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI))]
public static class Patch_ResourceReadout_ResourceReadoutOnGUI
{
    public static bool Prefix()
    {
        return !ReadoutPanel.OnGUI();
    }
}
