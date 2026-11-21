using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>
/// The grouped mode's groups, which nest: built-in groups found from item categories, names and drug types, plus the player's own.
/// A thing joins the deepest group that claims it; players can move things, and move or nest any group.
/// </summary>
public static class ReadoutGroups
{
    public const string Other = "Other";
    private const string Drugs = "Drugs";
    private const string Ammo = "Ammo";
    private const string Waste = "Waste";

    // Mods' categories and things claimed by name: any ammo category, and waste, trash and garbage.
    private static readonly string[] WasteWords = { "Waste", "Trash", "Garbage" };
    private const string CustomPrefix = "Custom";
    private const int MaxDepth = 64;

    // Built-in groups in their usual order: key, icon, usual parent, categories claimed, things claimed by defName.
    private static readonly (string key, string icon, string? parent, string[] categories, string[] things)[] Builtin =
    {
        ("Essentials", "star", null, new string[0], new[] { "Silver", "Gold", "Steel", "Plasteel", "WoodLog", "ComponentIndustrial", "ComponentSpacer" }),
        ("Food", "carrot", null, new[] { "FoodRaw", "Foods" }, new string[0]),
        ("Meat", "bone", "Food", new[] { "MeatRaw" }, new string[0]),
        ("Vegetables", "plant", "Food", new[] { "PlantFoodRaw" }, new string[0]),
        ("AnimalProducts", "egg", "Food", new[] { "AnimalProductRaw" }, new string[0]),
        ("AnimalFeed", "paw-print", "Food", new string[0], new[] { "Hay", "Kibble" }),
        ("Meals", "bowl-food", null, new[] { "FoodMeals" }, new[] { "Pemmican", "Chocolate", "BabyFood" }),
        ("Medicine", "first-aid-kit", null, new[] { "Medicine" }, new[] { "HemogenPack" }),
        (Drugs, "pill", null, new[] { "Drugs" }, new string[0]),
        ("SocialDrugs", "beer-stein", Drugs, new string[0], new string[0]),
        ("HardDrugs", "flask", Drugs, new string[0], new string[0]),
        ("MedicalDrugs", "syringe", Drugs, new string[0], new string[0]),
        ("Serums", "test-tube", Drugs, new string[0], new string[0]),
        ("Textiles", "t-shirt", null, new[] { "Textiles" }, new string[0]),
        ("Materials", "cube", null, new[] { "ResourcesRaw", "StoneBlocks", "StoneChunks" }, new string[0]),
        ("PlantMatter", "leaf", "Materials", new[] { "PlantMatter" }, new string[0]),
        ("Manufactured", "wrench", null, new[] { "Manufactured" }, new string[0]),
        (Ammo, "crosshair", null, new[] { "Ammo" }, new string[0]),
        ("Shells", "bomb", Ammo, new[] { "MortarShells" }, new string[0]),
        ("Items", "package", null, new[] { "Items", "ApparelUtility" }, new string[0]),
        ("BodyParts", "heartbeat", "Items", new[] { "BodyParts" }, new string[0]),
        (Waste, "trash", null, new string[0], new[] { "Wastepack" }),
        (Other, "squares-four", null, new string[0], new string[0]),
    };

    // Session lifetime: defs and categories never change while the game runs.
    private static Dictionary<ThingCategoryDef, string>? byCategory;
    private static Dictionary<string, string>? byDefName;
    private static readonly Dictionary<ThingDef, string> ByThing = new Dictionary<ThingDef, string>();

    // Rebuilt when settings change.
    private static readonly List<GroupInfo> Roots = new List<GroupInfo>();
    private static readonly List<GroupInfo> Flat = new List<GroupInfo>();
    private static readonly Dictionary<string, GroupInfo> ByKey = new Dictionary<string, GroupInfo>();
    private static readonly Dictionary<string, GroupInfo> Cache = new Dictionary<string, GroupInfo>();
    private static int builtFor = -1;

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    // Top-level groups in order.
    public static List<GroupInfo> TopLevel
    {
        get
        {
            Ensure();
            return Roots;
        }
    }

    // Every group, depth first, in the order the readout shows them.
    public static List<GroupInfo> All
    {
        get
        {
            Ensure();
            return Flat;
        }
    }

    public static GroupInfo? Find(string key)
    {
        Ensure();
        return ByKey.TryGetValue(key, out GroupInfo? group) ? group : null;
    }

    public static bool IsBuiltin(string key)
    {
        foreach ((string builtinKey, _, _, _, _) in Builtin)
        {
            if (builtinKey == key)
            {
                return true;
            }
        }

        return false;
    }

    // True when the group is the ancestor or somewhere inside it.
    public static bool IsWithin(string group, string ancestor)
    {
        GroupInfo? current = Find(group);
        for (int depth = 0; current != null && depth < MaxDepth; depth++)
        {
            if (current.key == ancestor)
            {
                return true;
            }

            current = current.parent;
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

        key = FindBuiltin(def);
        ByThing[def] = key;
        return key;
    }

    private static string FindBuiltin(ThingDef def)
    {
        BuildMaps();
        if (byDefName!.TryGetValue(def.defName, out string? named))
        {
            return named;
        }

        string key = Other;
        List<ThingCategoryDef>? categories = def.thingCategories;
        if (categories != null)
        {
            foreach (ThingCategoryDef first in categories)
            {
                ThingCategoryDef? current = first;
                for (int depth = 0; current != null && depth < MaxDepth; depth++)
                {
                    if (byCategory!.TryGetValue(current, out string? found))
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

        // Things no category claims go to Waste when their name says so (meat named after a waste animal keeps its category).
        if (key == Other && IsWaste(def.defName))
        {
            return Waste;
        }

        // Drugs split by kind; serums and anything without a drug kind (mech serums, catalysts) go to Serums.
        if (key == Drugs)
        {
            if (def.defName.EndsWith("Serum"))
            {
                return "Serums";
            }

            switch (def.ingestible?.drugCategory)
            {
                case DrugCategory.Social:
                    return "SocialDrugs";
                case DrugCategory.Hard:
                    return "HardDrugs";
                case DrugCategory.Medical:
                    return "MedicalDrugs";
                default:
                    return "Serums";
            }
        }

        return key;
    }

    private static bool IsWaste(string defName)
    {
        foreach (string word in WasteWords)
        {
            if (defName.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void BuildMaps()
    {
        if (byCategory != null && byDefName != null)
        {
            return;
        }

        byCategory = new Dictionary<ThingCategoryDef, string>();
        byDefName = new Dictionary<string, string>();
        foreach ((string key, _, _, string[] categories, string[] things) in Builtin)
        {
            foreach (string defName in categories)
            {
                ThingCategoryDef? category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
                if (category != null && !byCategory.ContainsKey(category))
                {
                    byCategory[category] = key;
                }
            }

            foreach (string defName in things)
            {
                byDefName[defName] = key;
            }
        }

        foreach (ThingCategoryDef category in DefDatabase<ThingCategoryDef>.AllDefsListForReading)
        {
            if (byCategory.ContainsKey(category))
            {
                continue;
            }

            if (category.defName.IndexOf(Ammo, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                byCategory[category] = Ammo;
            }
            else if (IsWaste(category.defName))
            {
                byCategory[category] = Waste;
            }
        }
    }

    // ---- Changes ------------------------------------------------------------------------

    public static string NewGroup(string label, string? parent = null)
    {
        string key = CustomPrefix + Settings.nextGroupId++;
        Settings.customGroups.Add(new CustomGroup { key = key, label = label, parent = parent ?? "" });
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

    // Moves a group under another (null for the top level); false when that would put it inside itself.
    public static bool SetParent(string key, string? parent)
    {
        if (parent != null && IsWithin(parent, key))
        {
            return false;
        }

        CustomGroup? custom = Settings.customGroups.Find(g => g.key == key);
        if (custom != null)
        {
            custom.parent = parent ?? "";
        }
        else if (parent == UsualParent(key))
        {
            Settings.groupParents.Remove(key);
        }
        else
        {
            Settings.groupParents[key] = parent ?? "";
        }

        return true;
    }

    // Removes a player's group: its things go back to their built-in groups, its child groups to its parent.
    public static void Delete(string key)
    {
        GroupInfo? info = Find(key);
        string? parent = info?.parent?.key;
        if (info != null)
        {
            foreach (GroupInfo child in info.children)
            {
                SetParent(child.key, parent);
            }
        }

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

    private static string? UsualParent(string key)
    {
        foreach ((string builtinKey, _, string? parent, _, _) in Builtin)
        {
            if (builtinKey == key)
            {
                return parent;
            }
        }

        return null;
    }

    // ---- Building the tree --------------------------------------------------------------

    private static void Ensure()
    {
        if (builtFor == Settings.version)
        {
            return;
        }

        builtFor = Settings.version;
        Roots.Clear();
        Flat.Clear();
        ByKey.Clear();
        List<GroupInfo> natural = new List<GroupInfo>();
        Dictionary<GroupInfo, string?> wanted = new Dictionary<GroupInfo, string?>();
        foreach ((string key, string icon, string? parent, _, _) in Builtin)
        {
            string label = Settings.groupLabels.TryGetValue(key, out string? renamed) ? renamed : ("RRP_Group_" + key).Translate().ToString();
            GroupInfo group = Add(key, label, icon, custom: false);
            natural.Add(group);
            wanted[group] = Settings.groupParents.TryGetValue(key, out string? moved) ? (moved.Length == 0 ? null : moved) : parent;
        }

        foreach (CustomGroup custom in Settings.customGroups)
        {
            if (!custom.key.NullOrEmpty() && !ByKey.ContainsKey(custom.key))
            {
                GroupInfo group = Add(custom.key, custom.label, "folder", custom: true);
                natural.Add(group);
                wanted[group] = custom.parent.NullOrEmpty() ? null : custom.parent;
            }
        }

        // Parents that are missing or would make a loop fall back to the top level.
        foreach (GroupInfo group in natural)
        {
            string? parentKey = wanted[group];
            group.parent = parentKey != null && ByKey.TryGetValue(parentKey, out GroupInfo? parent) && !Loops(group, parent, wanted) ? parent : null;
        }

        Dictionary<string, int> ranks = new Dictionary<string, int>();
        for (int i = 0; i < Settings.groupOrder.Count; i++)
        {
            ranks[Settings.groupOrder[i]] = i;
        }

        // A group missing from a saved order (a new built-in one) follows the group listed before it.
        List<(GroupInfo group, int rank, int index)> keyed = new List<(GroupInfo, int, int)>();
        int previous = -1;
        for (int i = 0; i < natural.Count; i++)
        {
            previous = ranks.TryGetValue(natural[i].rowKey, out int rank) ? rank : previous;
            keyed.Add((natural[i], previous, i));
        }

        keyed.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.index.CompareTo(b.index));
        foreach ((GroupInfo group, _, _) in keyed)
        {
            (group.parent?.children ?? Roots).Add(group);
        }

        foreach (GroupInfo root in Roots)
        {
            Flatten(root, 0);
        }
    }

    private static bool Loops(GroupInfo group, GroupInfo parent, Dictionary<GroupInfo, string?> wanted)
    {
        GroupInfo? current = parent;
        for (int depth = 0; current != null && depth < MaxDepth; depth++)
        {
            if (current == group)
            {
                return true;
            }

            string? next = wanted.TryGetValue(current, out string? key) ? key : null;
            current = next != null && ByKey.TryGetValue(next, out GroupInfo? found) ? found : null;
        }

        return current != null;
    }

    private static void Flatten(GroupInfo group, int depth)
    {
        group.depth = depth;
        Flat.Add(group);
        if (depth >= MaxDepth)
        {
            return;
        }

        foreach (GroupInfo child in group.children)
        {
            Flatten(child, depth + 1);
        }
    }

    private static GroupInfo Add(string key, string label, string icon, bool custom)
    {
        if (!Cache.TryGetValue(key, out GroupInfo? group))
        {
            group = new GroupInfo { key = key, rowKey = Keys.OfGroup(key), icon = CoreWidgets.Icon(icon), childList = Keys.GroupsOf(key) };
            Cache[key] = group;
        }

        group.label = label;
        group.custom = custom;
        group.parent = null;
        group.children.Clear();
        ByKey[key] = group;
        return group;
    }
}
