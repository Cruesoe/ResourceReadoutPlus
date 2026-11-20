using HarmonyLib;
using RimWorld;

namespace ResourceReadoutPlus;

// The base game's only place that changes stored counts; the readout rebuilds after it.
[HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts))]
public static class Patch_ResourceCounter_UpdateResourceCounts
{
    public static void Postfix()
    {
        ReadoutCounts.CountsUpdated();
    }
}
