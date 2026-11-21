using Verse;

namespace ResourceReadoutPlus;

/// <summary>Saved keys for rows: "T:" things, "C:" categories, "G:" groups, "F:" look-alike families (by category).</summary>
public static class Keys
{
    public const string Thing = "T:";
    public const string Category = "C:";
    public const string Group = "G:";
    public const string Family = "F:";

    // Parent key for the categorized top level.
    public const string Top = "TOP";

    // Drag lists that aren't a group or category.
    public const string PinnedList = "PIN";
    public const string SimpleList = "SIMPLE";

    // Child groups of a group (or the top level, with nothing after the colon).
    public const string GroupsList = "GROUPS:";

    public static string GroupsOf(string? groupKey) => GroupsList + (groupKey ?? "");

    // Session lifetime: defs never change while the game runs.
    private static readonly System.Collections.Generic.Dictionary<Def, string> Cached = new System.Collections.Generic.Dictionary<Def, string>();

    public static string Of(ThingDef def)
    {
        if (!Cached.TryGetValue(def, out string? key))
        {
            key = Thing + def.defName;
            Cached[def] = key;
        }

        return key;
    }

    public static string Of(ThingCategoryDef category)
    {
        if (!Cached.TryGetValue(category, out string? key))
        {
            key = Category + category.defName;
            Cached[category] = key;
        }

        return key;
    }

    public static string OfFamily(ThingCategoryDef category) => Family + category.defName;

    public static string OfGroup(string groupKey) => Group + groupKey;

    public static ThingDef? ThingOf(string key)
    {
        return key.StartsWith(Thing) ? DefDatabase<ThingDef>.GetNamedSilentFail(key.Substring(Thing.Length)) : null;
    }

    // The category behind a "C:" or "F:" key.
    public static ThingCategoryDef? CategoryOf(string key)
    {
        if (key.StartsWith(Category))
        {
            return DefDatabase<ThingCategoryDef>.GetNamedSilentFail(key.Substring(Category.Length));
        }

        return key.StartsWith(Family) ? DefDatabase<ThingCategoryDef>.GetNamedSilentFail(key.Substring(Family.Length)) : null;
    }

    public static string? GroupOf(string key)
    {
        return key.StartsWith(Group) ? key.Substring(Group.Length) : null;
    }
}
