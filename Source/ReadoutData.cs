using System;
using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>
/// The readout's rows for the current map, rebuilt when counts, settings, trends, the map or the search change.
/// Lifetime: game; cleared by Forget when a game is unloaded.
/// </summary>
public static partial class ReadoutData
{
    public static readonly List<Row> Rows = new List<Row>();
    public static readonly List<LowStock> Low = new List<LowStock>();

    // Every unit key of each drag list in order, hidden ones included, for saving a new order.
    public static readonly Dictionary<string, List<string>> ListUnits = new Dictionary<string, List<string>>();

    public static string search = "";
    public static int Version;
    private static readonly Predicate<ThingDef> SearchHitDef = SearchHit;

    private static Map? builtMap;
    private static int builtStamp = -1;
    private static int builtSettings = -1;
    private static int builtTrends = -1;
    private static int builtHour = -1;
    private static int builtMarks = -1;
    private static string builtSearch = "";
    private static float builtAt = -1f;
    private const float CountRebuildDelay = 0.5f;

    // Per build scratch, kept to avoid allocating every rebuild.
    private static readonly List<ThingDef> Resources = new List<ThingDef>();
    private static readonly Dictionary<ThingDef, int> Counts = new Dictionary<ThingDef, int>();
    private static readonly HashSet<string> LowKeys = new HashSet<string>();
    private static readonly HashSet<string> NearKeys = new HashSet<string>();
    private static readonly Dictionary<string, List<ThingDef>> ByGroup = new Dictionary<string, List<ThingDef>>();

    // Settings as sets, rebuilt when settings change.
    private static readonly HashSet<string> Hidden = new HashSet<string>();
    private static readonly HashSet<string> Pinned = new HashSet<string>();
    private static readonly HashSet<ThingCategoryDef> Families = new HashSet<ThingCategoryDef>();
    private static readonly HashSet<string> FoldedGroups = new HashSet<string>();
    private static readonly HashSet<string> OpenCategories = new HashSet<string>();
    private static readonly HashSet<string> OpenFamilies = new HashSet<string>();
    private static readonly Dictionary<string, int> Ranks = new Dictionary<string, int>();
    private static readonly Dictionary<ThingCategoryDef, string> FamilyKeys = new Dictionary<ThingCategoryDef, string>();
    private static int setsFor = -1;
    private static ReadoutMode ranksFor = (ReadoutMode)(-1);

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public static void Forget()
    {
        Rows.Clear();
        Low.Clear();
        ListUnits.Clear();
        builtMap = null;
        buildMap = null;
        TrendMemo.Clear();
        MatchMemo.Clear();
        search = "";
        ForgetCategorized();
    }

    public static void Ensure(Map map)
    {
        int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
        bool same = map == builtMap && builtSettings == Settings.version && builtTrends == ResourceTrends.Version && builtHour == hour && builtSearch == search
            && builtMarks == ResourceDiscoveries.Version;
        if (same && builtStamp == ReadoutCounts.Stamp)
        {
            return;
        }

        // Counts change whenever a stack is stored or taken, so rebuilds for counts alone wait a moment.
        float now = Time.realtimeSinceStartup;
        if (same && now - builtAt < CountRebuildDelay)
        {
            return;
        }

        builtAt = now;
        builtMap = map;
        builtStamp = ReadoutCounts.Stamp;
        builtSettings = Settings.version;
        builtTrends = ResourceTrends.Version;
        builtHour = hour;
        builtSearch = search;
        builtMarks = ResourceDiscoveries.Version;
        Build(map);
        Version++;
    }

    public static string FamilyKey(ThingCategoryDef category)
    {
        if (!FamilyKeys.TryGetValue(category, out string? key))
        {
            key = Keys.OfFamily(category);
            FamilyKeys[category] = key;
        }

        return key;
    }

    // The family a thing is shown in, or null when its category isn't combined.
    public static ThingCategoryDef? FamilyOf(ThingDef def)
    {
        EnsureSets();
        ThingCategoryDef? first = def.FirstThingCategory;
        return first != null && Families.Contains(first) ? first : null;
    }

    public static bool IsHidden(string key)
    {
        EnsureSets();
        return Hidden.Contains(key);
    }

    public static bool IsPinned(string key)
    {
        EnsureSets();
        return Pinned.Contains(key);
    }

    private static void EnsureSets()
    {
        if (setsFor == Settings.version)
        {
            return;
        }

        setsFor = Settings.version;
        Fill(Hidden, Settings.hidden);
        Fill(Pinned, Settings.pinned);
        Fill(FoldedGroups, Settings.foldedGroups);
        Fill(OpenCategories, Settings.openCategories);
        Fill(OpenFamilies, Settings.openFamilies);
        Families.Clear();
        foreach (string defName in Settings.families)
        {
            if (DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName) is ThingCategoryDef category)
            {
                Families.Add(category);
            }
        }

        ranksFor = (ReadoutMode)(-1);
    }

    private static void Fill(HashSet<string> set, List<string> source)
    {
        set.Clear();
        foreach (string key in source)
        {
            set.Add(key);
        }
    }

    private static void EnsureRanks(ReadoutMode mode)
    {
        if (ranksFor == mode)
        {
            return;
        }

        ranksFor = mode;
        Ranks.Clear();
        List<string> order = Settings.OrderFor(mode);
        for (int i = 0; i < order.Count; i++)
        {
            Ranks[order[i]] = i;
        }
    }

    private static void Build(Map map)
    {
        EnsureSets();
        Rows.Clear();
        ListUnits.Clear();
        TrendMemo.Clear();
        MatchMemo.Clear();
        buildMap = map;
        filtering = Settings.Filtering;
        sorting = Settings.Sorting;
        ResourceCounter counter = map.resourceCounter;

        Resources.Clear();
        Counts.Clear();
        foreach (ThingDef def in counter.AllCountedAmounts.Keys)
        {
            Resources.Add(def);
        }

        foreach (ThingDef def in Resources)
        {
            Counts[def] = ReadoutCounts.Count(map, def);
        }

        ByGroup.Clear();
        foreach (ThingDef def in Resources)
        {
            string group = ReadoutGroups.GroupOf(def);
            if (!ByGroup.TryGetValue(group, out List<ThingDef>? list))
            {
                list = new List<ThingDef>();
                ByGroup[group] = list;
            }

            list.Add(def);
        }

        BuildLow(map);
        BuildPinned(map);

        if (!search.NullOrEmpty())
        {
            BuildSearch(map);
            return;
        }

        switch (Settings.mode)
        {
            case ReadoutMode.Simple:
                BuildSimple(map);
                break;
            case ReadoutMode.Categorized:
                BuildCategorized(map);
                break;
            default:
                BuildGrouped(map);
                break;
        }
    }

    // ---- Totals and minimum stock -------------------------------------------------------

    private static int CountOf(ThingDef def)
    {
        return Counts.TryGetValue(def, out int count) ? count : 0;
    }

    private static bool InTotal(string key)
    {
        return Settings.countHiddenInTotals || !Hidden.Contains(key);
    }

    // A thing counts towards its group when neither it nor its look-alike row is hidden.
    private static bool ThingInTotal(ThingDef def)
    {
        return InTotal(Keys.Of(def)) && (FamilyOf(def) is not ThingCategoryDef family || InTotal(FamilyKey(family)));
    }

    private static int FamilyTotal(ThingCategoryDef family)
    {
        int total = 0;
        foreach (ThingDef def in Resources)
        {
            if (FamilyOf(def) == family && InTotal(Keys.Of(def)))
            {
                total += CountOf(def);
            }
        }

        return total;
    }

    // A group's total, its child groups included.
    private static int GroupTotal(GroupInfo group)
    {
        int total = 0;
        if (ByGroup.TryGetValue(group.key, out List<ThingDef>? defs))
        {
            foreach (ThingDef def in defs)
            {
                if (ThingInTotal(def))
                {
                    total += CountOf(def);
                }
            }
        }

        foreach (GroupInfo child in group.children)
        {
            if (InTotal(child.rowKey))
            {
                total += GroupTotal(child);
            }
        }

        return total;
    }

    // Every row with a minimum is checked, folded or not, so the alert sees them all.
    private static void BuildLow(Map map)
    {
        Low.Clear();
        LowKeys.Clear();
        NearKeys.Clear();
        foreach (KeyValuePair<string, int> entry in Settings.minimums)
        {
            string key = entry.Key;
            int total;
            string label;
            if (Keys.ThingOf(key) is ThingDef thing)
            {
                total = CountOf(thing);
                label = thing.LabelCap;
            }
            else if (key.StartsWith(Keys.Family, StringComparison.Ordinal) && Keys.CategoryOf(key) is ThingCategoryDef family)
            {
                total = FamilyTotal(family);
                label = family.LabelCap;
            }
            else if (Keys.CategoryOf(key) is ThingCategoryDef category)
            {
                total = CategoryTotal(map, category);
                label = category.LabelCap;
            }
            else if (Keys.GroupOf(key) is string group && ReadoutGroups.Find(group) is GroupInfo info)
            {
                total = GroupTotal(info);
                label = info.label;
            }
            else
            {
                continue;
            }

            if (total < entry.Value)
            {
                LowKeys.Add(key);
                Low.Add(new LowStock { label = label, count = total, minimum = entry.Value });
            }
            else if (total < NearLimit(entry.Value))
            {
                NearKeys.Add(key);
            }
        }

        Low.SortBy(low => low.label);
    }

    // ---- Units: things and look-alike families ------------------------------------------

    // Things and look-alike families in their saved order; pinned rows are left out unless asked for.
    public static List<ReadoutUnit> UnitsOf(List<ThingDef> defs, ReadoutMode mode, bool withPinned = false)
    {
        EnsureSets();
        List<ReadoutUnit> units = new List<ReadoutUnit>();
        Dictionary<ThingCategoryDef, ReadoutUnit>? families = null;
        foreach (ThingDef def in defs)
        {
            if (!withPinned && Pinned.Contains(Keys.Of(def)))
            {
                continue;
            }

            ThingCategoryDef? family = FamilyOf(def);
            if (family == null)
            {
                units.Add(new ReadoutUnit { key = Keys.Of(def), def = def, index = units.Count });
                continue;
            }

            families ??= new Dictionary<ThingCategoryDef, ReadoutUnit>();
            if (!families.TryGetValue(family, out ReadoutUnit? unit))
            {
                unit = new ReadoutUnit { key = FamilyKey(family), family = family, index = units.Count };
                families[family] = unit;
                if (withPinned || !Pinned.Contains(unit.key))
                {
                    units.Add(unit);
                }
            }

            unit.members.Add(def);
        }

        EnsureRanks(mode);
        if (Ranks.Count > 0)
        {
            units.Sort((a, b) =>
            {
                int rankA = Ranks.TryGetValue(a.key, out int ra) ? ra : int.MaxValue;
                int rankB = Ranks.TryGetValue(b.key, out int rb) ? rb : int.MaxValue;
                return rankA != rankB ? rankA.CompareTo(rankB) : a.index.CompareTo(b.index);
            });
        }

        return units;
    }

    private static List<string> UnitKeys(List<ReadoutUnit> units)
    {
        List<string> keys = new List<string>(units.Count);
        foreach (ReadoutUnit unit in units)
        {
            keys.Add(unit.key);
        }

        return keys;
    }

    // Shown when stocked, set to show at zero or below its minimum; while filtering, shown when it matches instead.
    private static bool Visible(ThingDef def, int count)
    {
        string key = Keys.Of(def);
        if (Hidden.Contains(key))
        {
            return false;
        }

        if (filtering)
        {
            return Matches(def);
        }

        return count > 0 || def.resourceReadoutAlwaysShow || (Settings.showZero && def.PlayerAcquirable) || LowKeys.Contains(key);
    }

    private static bool UnitVisible(ReadoutUnit unit)
    {
        if (unit.def != null)
        {
            return Visible(unit.def, CountOf(unit.def));
        }

        return !Hidden.Contains(unit.key) && (FirstVisible(unit) != null || (filtering && LowMatch(unit.key)));
    }

    private static ThingDef? FirstVisible(ReadoutUnit unit)
    {
        foreach (ThingDef member in unit.members)
        {
            if (Visible(member, CountOf(member)))
            {
                return member;
            }
        }

        return null;
    }

    private static bool UnitLow(ReadoutUnit unit)
    {
        if (LowKeys.Contains(unit.key))
        {
            return true;
        }

        foreach (ThingDef member in unit.members)
        {
            if (LowKeys.Contains(Keys.Of(member)))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddUnit(Map map, ReadoutUnit unit, int depth, string list)
    {
        if (unit.def != null)
        {
            AddThing(map, unit.def, depth, list, unit.key, draggable: true);
            return;
        }

        int visible = 0;
        ThingDef? only = null;
        foreach (ThingDef member in unit.members)
        {
            if (Visible(member, CountOf(member)))
            {
                visible++;
                only = member;
            }
        }

        // A family with one kind on show is drawn as that kind.
        if (visible == 1 && only != null)
        {
            AddThing(map, only, depth, list, unit.key, draggable: true);
            return;
        }

        ThingCategoryDef family = unit.family!;
        Row row = new Row
        {
            kind = RowKind.Family,
            key = unit.key,
            label = family.LabelCap,
            depth = depth,
            def = FirstVisible(unit) ?? unit.members[0],
            category = family,
            members = unit.members,
            count = FamilyTotal(family),
            expandable = true,
            open = filtering || OpenFamilies.Contains(unit.key),
            low = UnitLow(unit),
            draggable = true,
            list = list,
            unit = unit.key,
            marked = AnyMarked(unit.members),
        };
        Finish(row);
        Rows.Add(row);

        if (row.open)
        {
            foreach (ThingDef member in unit.members)
            {
                if (Visible(member, CountOf(member)))
                {
                    AddThing(map, member, depth + 1, list, unit.key, draggable: false);
                }
            }
        }
    }

    private static void AddThing(Map map, ThingDef def, int depth, string list, string unit, bool draggable)
    {
        string key = Keys.Of(def);
        Row row = new Row
        {
            kind = RowKind.Item,
            key = key,
            label = def.LabelCap,
            depth = depth,
            def = def,
            count = CountOf(def),
            low = LowKeys.Contains(key),
            draggable = draggable,
            list = list,
            unit = unit,
            mark = MarkOf(def),
        };

        TrendInfo trend = TrendOf(def);
        row.hasChange = trend.has;
        row.change = trend.change;
        row.trend = trend.trend;
        row.ticksLeft = trend.ticksLeft;
        if (Settings.runOutRow && trend.ticksLeft >= 0 && trend.ticksLeft <= RunOutRowMost)
        {
            row.runOutText = RunOutShort(trend.ticksLeft);
        }

        Finish(row);
        Rows.Add(row);
    }

    // A change worth an arrow: at least one, and at least 2% of where it started.
    private static bool Significant(int change, int from)
    {
        return change != 0 && Math.Abs(change) >= Math.Max(1, Math.Abs(from) / 50);
    }

    private static void Finish(Row row)
    {
        row.countText = Format(row.count);
        row.minimum = Settings.minimums.TryGetValue(row.key, out int minimum) ? minimum : -1;
        row.near = NearKeys.Contains(row.key);
        if (filtering || sorting)
        {
            row.draggable = false;
        }

        row.tipTitle = () => row.label;
        row.tipBody = () => ReadoutTips.Body(row);
    }

    public static string Format(int count)
    {
        return Settings.shortNumbers ? CoreWidgets.ShortAmount(count) : count.ToStringCached();
    }

    // ---- Pinned, search, simple and grouped ---------------------------------------------

    private static void BuildPinned(Map map)
    {
        if (Settings.pinned.Count == 0)
        {
            return;
        }

        List<string> keys = new List<string>();
        int start = Rows.Count;
        foreach (string key in Settings.pinned)
        {
            if (Keys.ThingOf(key) is ThingDef thing && Counts.ContainsKey(thing))
            {
                keys.Add(key);
                if ((!filtering || Settings.pinnedIgnoreFilter || Matches(thing)) && SearchHit(thing))
                {
                    AddThing(map, thing, 0, Keys.PinnedList, key, draggable: true);
                }
            }
            else if (key.StartsWith(Keys.Family, StringComparison.Ordinal) && Keys.CategoryOf(key) is ThingCategoryDef family)
            {
                ReadoutUnit unit = new ReadoutUnit { key = key, family = family };
                foreach (ThingDef def in Resources)
                {
                    if (def.FirstThingCategory == family)
                    {
                        unit.members.Add(def);
                    }
                }

                if (unit.members.Count > 0)
                {
                    keys.Add(key);
                    if ((!filtering || Settings.pinnedIgnoreFilter || LowMatch(key) || unit.members.Exists(MatchesDef)) && unit.members.Exists(SearchHitDef))
                    {
                        AddPinnedFamily(map, unit);
                    }
                }
            }
        }

        ListUnits[Keys.PinnedList] = keys;
        if (Rows.Count > start)
        {
            Rows[Rows.Count - 1].pinnedEnd = true;
        }
    }

    // Pinned families stay on show at zero, as pinned things do.
    private static void AddPinnedFamily(Map map, ReadoutUnit unit)
    {
        ThingCategoryDef family = unit.family!;
        Row row = new Row
        {
            kind = RowKind.Family,
            key = unit.key,
            label = family.LabelCap,
            depth = 0,
            def = FirstVisible(unit) ?? unit.members[0],
            category = family,
            members = unit.members,
            count = FamilyTotal(family),
            expandable = true,
            open = OpenFamilies.Contains(unit.key),
            low = UnitLow(unit),
            draggable = true,
            list = Keys.PinnedList,
            unit = unit.key,
            marked = AnyMarked(unit.members),
        };
        Finish(row);
        Rows.Add(row);
        if (row.open)
        {
            foreach (ThingDef member in unit.members)
            {
                if (Visible(member, CountOf(member)))
                {
                    AddThing(map, member, 1, Keys.PinnedList, unit.key, draggable: false);
                }
            }
        }
    }

    // Hidden rows stay out, and pinned ones already show above, filtered by the search.
    private static void BuildSearch(Map map)
    {
        foreach (ThingDef def in Resources)
        {
            string key = Keys.Of(def);
            if (Pinned.Contains(key) || (FamilyOf(def) is ThingCategoryDef family && Pinned.Contains(FamilyKey(family))))
            {
                continue;
            }

            if (SearchHit(def) && Visible(def, CountOf(def)))
            {
                AddThing(map, def, 0, "", key, draggable: false);
            }
        }
    }

    private static bool SearchHit(ThingDef def)
    {
        return search.NullOrEmpty() || (def.label != null && def.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void BuildSimple(Map map)
    {
        List<ReadoutUnit> units = UnitsOf(Resources, ReadoutMode.Simple);
        ListUnits[Keys.SimpleList] = UnitKeys(units);
        SortByChange(units);
        foreach (ReadoutUnit unit in units)
        {
            if (UnitVisible(unit))
            {
                AddUnit(map, unit, 0, Keys.SimpleList);
            }
        }
    }

    private static void BuildGrouped(Map map)
    {
        AddGroupLevel(map, ReadoutGroups.TopLevel, Keys.GroupsOf(null));
    }

    private static void AddGroupLevel(Map map, List<GroupInfo> groups, string list)
    {
        List<string> keys = new List<string>(groups.Count);
        foreach (GroupInfo group in groups)
        {
            keys.Add(group.rowKey);
        }

        ListUnits[list] = keys;
        foreach (GroupInfo group in groups)
        {
            AddGroup(map, group, list);
        }
    }

    // Adds a group's header, then (when open) its child groups and its own things.
    private static void AddGroup(Map map, GroupInfo group, string list)
    {
        if (Hidden.Contains(group.rowKey) || !GroupShown(group, 0))
        {
            return;
        }

        List<ThingDef> defs = ByGroup.TryGetValue(group.key, out List<ThingDef>? found) ? found : new List<ThingDef>();
        List<ReadoutUnit> units = UnitsOf(defs, ReadoutMode.Grouped);
        ListUnits[group.rowKey] = UnitKeys(units);
        SortByChange(units);

        Row header = new Row
        {
            kind = RowKind.Group,
            key = group.rowKey,
            label = group.label,
            icon = group.icon,
            depth = group.depth,
            count = GroupTotal(group),
            expandable = true,
            open = filtering || !FoldedGroups.Contains(group.rowKey),
            low = GroupLow(group, 0),
            marked = GroupMarked(group, 0),
            chartDefs = ChartDefsOf(group),
            draggable = true,
            list = list,
            unit = group.rowKey,
            opens = group.rowKey,
        };
        Finish(header);
        Rows.Add(header);
        if (!header.open)
        {
            return;
        }

        AddGroupLevel(map, group.children, group.childList);
        foreach (ReadoutUnit unit in units)
        {
            if (UnitVisible(unit))
            {
                AddUnit(map, unit, group.depth + 1, group.rowKey);
            }
        }
    }

    // A group shows when anything inside it shows; the player's own groups show even when empty, so things can be dropped into them.
    private static bool GroupShown(GroupInfo group, int depth)
    {
        if (filtering ? LowMatch(group.rowKey) : group.custom || LowKeys.Contains(group.rowKey))
        {
            return true;
        }

        if (ByGroup.TryGetValue(group.key, out List<ThingDef>? defs))
        {
            foreach (ThingDef def in defs)
            {
                if (!Pinned.Contains(Keys.Of(def)) && Visible(def, CountOf(def)))
                {
                    return true;
                }
            }
        }

        if (depth < MaxDepth)
        {
            foreach (GroupInfo child in group.children)
            {
                if (!Hidden.Contains(child.rowKey) && GroupShown(child, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool GroupLow(GroupInfo group, int depth)
    {
        if (LowKeys.Count == 0)
        {
            return false;
        }

        if (LowKeys.Contains(group.rowKey))
        {
            return true;
        }

        if (ByGroup.TryGetValue(group.key, out List<ThingDef>? defs))
        {
            foreach (ThingDef def in defs)
            {
                if (LowKeys.Contains(Keys.Of(def)) || (FamilyOf(def) is ThingCategoryDef family && LowKeys.Contains(FamilyKey(family))))
                {
                    return true;
                }
            }
        }

        if (depth < MaxDepth)
        {
            foreach (GroupInfo child in group.children)
            {
                if (GroupLow(child, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
