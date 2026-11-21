using HarmonyLib;
using RimWorld;
using Verse;

namespace ResourceReadoutPlus;

// The base game's only place that changes stored counts; the readout rebuilds after it, and new resources are marked.
[HarmonyPatch(typeof(ResourceCounter), nameof(ResourceCounter.UpdateResourceCounts))]
public static class Patch_ResourceCounter_UpdateResourceCounts
{
    public static void Postfix(ResourceCounter __instance)
    {
        ReadoutCounts.CountsUpdated();
        if (ResourceDiscoveries.Current is not ResourceDiscoveries discoveries)
        {
            return;
        }

        foreach (Map map in Find.Maps)
        {
            if (map.resourceCounter == __instance)
            {
                discoveries.Update(map);
                return;
            }
        }
    }
}
