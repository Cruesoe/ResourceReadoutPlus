using HarmonyLib;

namespace ResourceReadoutPlus;

// The base game calls this when a game is unloaded; per-game caches are cleared with it.
[HarmonyPatch(typeof(Verse.Profile.MemoryUtility), nameof(Verse.Profile.MemoryUtility.ClearAllMapsAndWorld))]
public static class Patch_MemoryUtility_ClearAllMapsAndWorld
{
    public static void Postfix()
    {
        ReadoutData.Forget();
        ReadoutCounts.Forget();
        ReadoutJump.Forget();
        ReadoutPanel.Forget();
    }
}
