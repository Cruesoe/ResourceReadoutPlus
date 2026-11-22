using System.Collections.Generic;
using Verse;

namespace ResourceReadoutPlus;

// The category tree, with rows the player moved between categories or to the top level.
public static partial class ReadoutData
{
    private const int MaxDepth = 64;

    // Rebuilt when settings change.
    private static readonly Dictionary<Def, ThingCategoryDef?> Moved = new Dictionary<Def, ThingCategoryDef?>();
    private static readonly Dictionary<ThingCategoryDef, List<Def>> MovedInto = new Dictionary<ThingCategoryDef, List<Def>>();
    private static readonly List<Def> MovedToTop = new List<Def>();
    private static readonly Dictionary<ThingCategoryDef, List<Def>> ChildrenCache = new Dictionary<ThingCategoryDef, List<Def>>();
    private static readonly Dictionary<ThingCategoryDef, bool> HasResourcesCache = new Dictionary<ThingCategoryDef, bool>();
    private static readonly List<Def> TopLevelCache = new List<Def>();
    private static int treeFor = -1;

    // Per build.
    private static readonly Dictionary<ThingCategoryDef, int> CategoryTotals = new Dictionary<ThingCategoryDef, int>();
    private static Map? totalsMap;
    private static int totalsStamp = -1;

    private static void ForgetCategorized()
    {
        CategoryTotals.Clear();
        totalsMap = null;
    }

    private static void BuildCategorized(Map map)
    {
        EnsureTree();
        List<Def> top = Sorted(TopLevelCache);
        ListUnits[Keys.Top] = KeysOf(top);
        SortLevel(top);
        AddLevel(map, top, 0, Keys.Top);
    }

    private static void AddLevel(Map map, List<Def> level, int depth, string list)
    {
        foreach (Def child in level)
        {
            if (child is ThingCategoryDef category)
            {
                string key = Keys.Of(category);
                int total = CategoryTotal(map, category);
                bool shown = filtering ? CategoryMatches(category, 0) : total > 0 || (Settings.showZero && HasResources(category)) || LowKeys.Contains(key) || AnyLowInside(category, 0);
                if (Hidden.Contains(key) || !shown)
                {
                    continue;
                }

                Row row = new Row
                {
                    kind = RowKind.Category,
                    key = key,
                    label = category.LabelCap,
                    depth = depth,
                    category = category,
                    icon = category.icon,
                    count = total,
                    expandable = true,
                    open = filtering || OpenCategories.Contains(key),
                    low = LowKeys.Contains(key) || AnyLowInside(category, 0),
                    marked = CategoryMarked(category, 0),
                    chartDefs = ChartDefsOf(category),
                    draggable = true,
                    list = list,
                    unit = key,
                    opens = key,
                };
                Finish(row);
                Rows.Add(row);

                if (row.open)
                {
                    List<Def> children = Sorted(Children(category));
                    ListUnits[key] = KeysOf(children);
                    SortLevel(children);
                    AddLevel(map, children, depth + 1, key);
                }
            }
            else if (child is ThingDef thing && thing.PlayerAcquirable)
            {
                string key = Keys.Of(thing);
                if (!Pinned.Contains(key) && Visible(thing, CountOf(thing)))
                {
                    AddThing(map, thing, depth, list, key, draggable: true);
                }
            }
        }
    }

    private static bool AnyLowInside(ThingCategoryDef category, int depth)
    {
        if (LowKeys.Count == 0 || depth > MaxDepth)
        {
            return false;
        }

        foreach (Def child in Children(category))
        {
            if (child is ThingCategoryDef inner ? LowKeys.Contains(Keys.Of(inner)) || AnyLowInside(inner, depth + 1) : LowKeys.Contains(Keys.Of((ThingDef)child)))
            {
                return true;
            }
        }

        return false;
    }

    // A category's total as the tree shows it, with moved rows counted where they now sit.
    private static int CategoryTotal(Map map, ThingCategoryDef category)
    {
        EnsureTree();
        if (totalsMap != map || totalsStamp != Version)
        {
            totalsMap = map;
            totalsStamp = Version;
            CategoryTotals.Clear();
        }

        return CategoryTotal(category, 0);
    }

    private static int CategoryTotal(ThingCategoryDef category, int depth)
    {
        if (CategoryTotals.TryGetValue(category, out int cached))
        {
            return cached;
        }

        int total = 0;
        if (depth <= MaxDepth)
        {
            foreach (Def child in Children(category))
            {
                if (child is ThingCategoryDef inner)
                {
                    if (InTotal(Keys.Of(inner)))
                    {
                        total += CategoryTotal(inner, depth + 1);
                    }
                }
                else if (child is ThingDef thing && InTotal(Keys.Of(thing)))
                {
                    total += CountOf(thing);
                }
            }
        }

        CategoryTotals[category] = total;
        return total;
    }

    private static List<Def> Sorted(List<Def> source)
    {
        List<Def> items = new List<Def>(source);
        EnsureRanks(ReadoutMode.Categorized);
        if (Ranks.Count == 0)
        {
            return items;
        }

        List<(Def def, int rank, int index)> keyed = new List<(Def, int, int)>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            keyed.Add((items[i], Ranks.TryGetValue(KeyOf(items[i]), out int rank) ? rank : int.MaxValue, i));
        }

        keyed.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.index.CompareTo(b.index));
        for (int i = 0; i < keyed.Count; i++)
        {
            items[i] = keyed[i].def;
        }

        return items;
    }

    private static string KeyOf(Def def)
    {
        return def is ThingCategoryDef category ? Keys.Of(category) : Keys.Of((ThingDef)def);
    }

    private static List<string> KeysOf(List<Def> defs)
    {
        List<string> keys = new List<string>(defs.Count);
        foreach (Def def in defs)
        {
            keys.Add(KeyOf(def));
        }

        return keys;
    }

    private static List<Def> Children(ThingCategoryDef category)
    {
        EnsureTree();
        if (ChildrenCache.TryGetValue(category, out List<Def>? children))
        {
            return children;
        }

        children = new List<Def>();
        foreach (ThingCategoryDef child in category.childCategories)
        {
            if (!child.resourceReadoutRoot && !Moved.ContainsKey(child))
            {
                children.Add(child);
            }
        }

        foreach (ThingDef thing in category.childThingDefs)
        {
            if (!Moved.ContainsKey(thing))
            {
                children.Add(thing);
            }
        }

        if (MovedInto.TryGetValue(category, out List<Def>? added))
        {
            children.AddRange(added);
        }

        ChildrenCache[category] = children;
        return children;
    }

    // Categories that could never hold a counted resource stay hidden even with zero counts shown.
    private static bool HasResources(ThingCategoryDef category)
    {
        if (HasResourcesCache.TryGetValue(category, out bool has))
        {
            return has;
        }

        HasResourcesCache[category] = false;
        foreach (Def child in Children(category))
        {
            if (child is ThingCategoryDef inner ? HasResources(inner) : ((ThingDef)child).CountAsResource && ((ThingDef)child).PlayerAcquirable)
            {
                has = true;
                break;
            }
        }

        HasResourcesCache[category] = has;
        return has;
    }

    private static void EnsureTree()
    {
        if (treeFor == Settings.version)
        {
            return;
        }

        treeFor = Settings.version;
        ChildrenCache.Clear();
        HasResourcesCache.Clear();
        LoadMoves();

        TopLevelCache.Clear();
        foreach (ThingCategoryDef category in DefDatabase<ThingCategoryDef>.AllDefsListForReading)
        {
            if (category.resourceReadoutRoot && !Moved.ContainsKey(category))
            {
                TopLevelCache.Add(category);
            }
        }

        TopLevelCache.AddRange(MovedToTop);
    }

    private static void LoadMoves()
    {
        Moved.Clear();
        MovedInto.Clear();
        MovedToTop.Clear();
        foreach (KeyValuePair<string, string> entry in Settings.parentOf)
        {
            Def? def = (Def?)Keys.ThingOf(entry.Key) ?? (entry.Key.StartsWith(Keys.Category) ? Keys.CategoryOf(entry.Key) : null);
            if (def == null)
            {
                continue;
            }

            if (entry.Value == Keys.Top)
            {
                Moved[def] = null;
            }
            else if (Keys.CategoryOf(entry.Value) is ThingCategoryDef parent)
            {
                Moved[def] = parent;
            }
        }

        // A saved move that would put a category inside itself is ignored.
        List<Def> looping = new List<Def>();
        foreach (Def def in Moved.Keys)
        {
            if (def is ThingCategoryDef category && IsWithin(ParentOf(category), category))
            {
                looping.Add(def);
            }
        }

        foreach (Def def in looping)
        {
            Moved.Remove(def);
        }

        foreach (KeyValuePair<Def, ThingCategoryDef?> entry in Moved)
        {
            if (entry.Value == null)
            {
                MovedToTop.Add(entry.Key);
                continue;
            }

            if (!MovedInto.TryGetValue(entry.Value, out List<Def>? list))
            {
                list = new List<Def>();
                MovedInto[entry.Value] = list;
            }

            list.Add(entry.Key);
        }
    }

    // A row's saved parent if moved, else its first base-game category; null is the top level.
    private static ThingCategoryDef? ParentOf(Def def)
    {
        if (Moved.TryGetValue(def, out ThingCategoryDef? moved))
        {
            return moved;
        }

        if (def is ThingCategoryDef category)
        {
            return category.resourceReadoutRoot ? null : category.parent;
        }

        return (def as ThingDef)?.FirstThingCategory;
    }

    // True when start is the category or somewhere inside it.
    public static bool IsWithin(ThingCategoryDef? start, ThingCategoryDef category)
    {
        EnsureTree();
        ThingCategoryDef? current = start;
        for (int depth = 0; current != null && depth < MaxDepth; depth++)
        {
            if (current == category)
            {
                return true;
            }

            current = ParentOf(current);
        }

        return false;
    }

    // Whether a row placed under this parent sits where the base game puts it, so no move needs saving.
    public static bool IsUsualParent(Def def, ThingCategoryDef? parent)
    {
        if (def is ThingCategoryDef category)
        {
            return parent == (category.resourceReadoutRoot ? null : category.parent);
        }

        // Any move pins a thing listed under several categories to one place.
        List<ThingCategoryDef>? categories = (def as ThingDef)?.thingCategories;
        return parent != null && categories != null && categories.Count == 1 && categories[0] == parent;
    }
}
