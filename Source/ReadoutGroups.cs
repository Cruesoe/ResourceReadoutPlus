using System.Collections.Generic;
using UIRethemed.Core;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>The grouped mode's groups: built-in ones found from item categories, plus the player's own.</summary>
public static class ReadoutGroups
{
    public const string Other = "Other";
    private const string CustomPrefix = "Custom";

    // Built-in groups in their usual order; a thing joins the first group that lists one of its categories, walking up from its own.
    private static readonly (string key, string icon, string[] categories)[] Builtin =
    {
        ("Materials", "cube", new[] { "StoneBlocks", "PlantMatter", "StoneChunks", "ResourcesRaw" }),
        ("Food", "carrot", new[] { "FoodRaw", "Foods" }),
        ("Meals", "bowl-food", new[] { "FoodMeals" }),
        ("Medicine", "first-aid-kit", new[] { "Medicine" }),
        ("Drugs", "pill", new[] { "Drugs" }),
        ("Textiles", "t-shirt", new[] { "Textiles" }),
        ("Manufactured", "wrench", new[] { "Manufactured" }),
        ("Items", "package", new[] { "Items" }),
        (Other, "squares-four", new string[0]),
    };

    private const int MaxDepth = 64;

    // Session lifetime: categories never change while the game runs.
    private static Dictionary<ThingCategoryDef, string>? byCategory;
    private static readonly Dictionary<ThingDef, string> ByThing = new Dictionary<ThingDef, string>();

    // Rebuilt when settings change.
    private static readonly List<GroupInfo> Ordered = new List<GroupInfo>();
    private static readonly Dictionary<string, GroupInfo> ByKey = new Dictionary<string, GroupInfo>();
    private static readonly Dictionary<string, GroupInfo> Cache = new Dictionary<string, GroupInfo>();
    private static int builtFor = -1;

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public static List<GroupInfo> All
    {
        get
        {
            Ensure();
            return Ordered;
        }
    }

    public static GroupInfo? Find(string key)
    {
        Ensure();
        return ByKey.TryGetValue(key, out GroupInfo? group) ? group : null;
    }

    public static bool IsBuiltin(string key)
    {
        foreach ((string builtinKey, _, _) in Builtin)
        {
            if (builtinKey == key)
            {
                return true;
            }
        }

        return false;
    }

    // The group a thing is in: the player's choice if that group still exists, else its built-in group.
    public static string GroupOf(ThingDef def)
    {
        Ensure();
        if (Settings.groupOf.TryGetValue(Keys.Of(def), out string? chosen) && ByKey.ContainsKey(chosen))
        {
            return chosen;
        }

        return BuiltinGroupOf(def);
    }

    public static string BuiltinGroupOf(ThingDef def)
    {
        if (ByThing.TryGetValue(def, out string? key))
        {
            return key;
        }

        key = Other;
        Dictionary<ThingCategoryDef, string> map = CategoryMap();
        List<ThingCategoryDef>? categories = def.thingCategories;
        if (categories != null)
        {
            foreach (ThingCategoryDef first in categories)
            {
                ThingCategoryDef? current = first;
                for (int depth = 0; current != null && depth < MaxDepth; depth++)
                {
                    if (map.TryGetValue(current, out string? found))
                    {
                        key = found;
                        break;
                    }

                    current = current.parent;
                }

                if (key != Other)
                {
                    break;
                }
            }
        }

        ByThing[def] = key;
        return key;
    }

    private static Dictionary<ThingCategoryDef, string> CategoryMap()
    {
        if (byCategory != null)
        {
            return byCategory;
        }

        byCategory = new Dictionary<ThingCategoryDef, string>();
        foreach ((string key, _, string[] categories) in Builtin)
        {
            foreach (string defName in categories)
            {
                ThingCategoryDef? category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
                if (category != null && !byCategory.ContainsKey(category))
                {
                    byCategory[category] = key;
                }
            }
        }

        return byCategory;
    }

    public static string NewGroup(string label)
    {
        string key = CustomPrefix + Settings.nextGroupId++;
        Settings.customGroups.Add(new CustomGroup { key = key, label = label });
        Settings.Changed();
        return key;
    }

    public static void Rename(string key, string label)
    {
        label = label.Trim();
        CustomGroup? custom = Settings.customGroups.Find(g => g.key == key);
        if (custom != null)
        {
            if (label.Length > 0)
            {
                custom.label = label;
            }
        }
        else if (label.Length == 0)
        {
            Settings.groupLabels.Remove(key);
        }
        else
        {
            Settings.groupLabels[key] = label;
        }

        Settings.Changed();
    }

    // Removes a player's group; its things go back to their built-in groups.
    public static void Delete(string key)
    {
        Settings.customGroups.RemoveAll(g => g.key == key);
        List<string> moved = new List<string>();
        foreach (KeyValuePair<string, string> entry in Settings.groupOf)
        {
            if (entry.Value == key)
            {
                moved.Add(entry.Key);
            }
        }

        foreach (string thing in moved)
        {
            Settings.groupOf.Remove(thing);
        }

        string rowKey = Keys.OfGroup(key);
        Settings.groupOrder.Remove(rowKey);
        Settings.foldedGroups.Remove(rowKey);
        Settings.hidden.Remove(rowKey);
        Settings.countAll.Remove(rowKey);
        Settings.minimums.Remove(rowKey);
        Settings.Changed();
    }

    private static void Ensure()
    {
        if (builtFor == Settings.version)
        {
            return;
        }

        builtFor = Settings.version;
        Ordered.Clear();
        ByKey.Clear();
        foreach ((string key, string icon, _) in Builtin)
        {
            string label = Settings.groupLabels.TryGetValue(key, out string? renamed) ? renamed : ("RRP_Group_" + key).Translate().ToString();
            Add(key, label, icon, custom: false);
        }

        foreach (CustomGroup group in Settings.customGroups)
        {
            if (!group.key.NullOrEmpty() && !ByKey.ContainsKey(group.key))
            {
                Add(group.key, group.label, "folder", custom: true);
            }
        }

        // Saved order first, then the rest in their usual order.
        Dictionary<string, int> ranks = new Dictionary<string, int>();
        for (int i = 0; i < Settings.groupOrder.Count; i++)
        {
            ranks[Settings.groupOrder[i]] = i;
        }

        List<(GroupInfo group, int rank, int index)> keyed = new List<(GroupInfo, int, int)>();
        for (int i = 0; i < Ordered.Count; i++)
        {
            keyed.Add((Ordered[i], ranks.TryGetValue(Ordered[i].rowKey, out int rank) ? rank : int.MaxValue, i));
        }

        keyed.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.index.CompareTo(b.index));
        Ordered.Clear();
        foreach ((GroupInfo group, _, _) in keyed)
        {
            Ordered.Add(group);
        }
    }

    private static void Add(string key, string label, string icon, bool custom)
    {
        if (!Cache.TryGetValue(key, out GroupInfo? group))
        {
            group = new GroupInfo { key = key, rowKey = Keys.OfGroup(key), icon = CoreWidgets.Icon(icon) };
            Cache[key] = group;
        }

        group.label = label;
        group.custom = custom;
        Ordered.Add(group);
        ByKey[key] = group;
    }
}
