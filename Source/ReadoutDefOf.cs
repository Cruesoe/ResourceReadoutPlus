using RimWorld;
using Verse;

namespace ResourceReadoutPlus;

[DefOf]
public static class ReadoutDefOf
{
    public static KeyBindingDef RRP_ToggleFilter = null!;

    static ReadoutDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ReadoutDefOf));
    }
}
