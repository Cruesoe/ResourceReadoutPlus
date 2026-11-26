using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

// The filter, sorting by change, new marks, run-out estimates and the defs behind a header's hover chart.
public static partial class ReadoutData
{
    // Run-out estimates show in the row only when they are this close.
    private const int RunOutRowMost = GenDate.TicksPerDay * 10;

    private struct TrendInfo
    {
        public bool has;
        public int change;
        public int trend;
        public int ticksLeft;
    }

    // Per build, cleared at the start of each.
    private static readonly Dictionary<ThingDef, TrendInfo> TrendMemo = new Dictionary<ThingDef, TrendInfo>();
    private static readonly Dictionary<ThingDef, bool> MatchMemo = new Dictionary<ThingDef, bool>();
    private static readonly Predicate<ThingDef> MatchesDef = Matches;
    private static Map? buildMap;
    private static bool filtering;
    private static bool sorting;

    // Minimums at or above which a total counts as near: the minimum plus the near percentage, rounded up.
    private static long NearLimit(int minimum)
    {
        return minimum + (long)Math.Ceiling(minimum * Settings.nearPercent / 100.0);
    }

    private static TrendInfo TrendOf(ThingDef def)
    {
        if (TrendMemo.TryGetValue(def, out TrendInfo info))
        {
            return info;
        }

        info.ticksLeft = -1;
        int count = CountOf(def);
        if (Settings.trends && buildMap != null && ResourceTrends.Current is ResourceTrends trends
            && trends.Change(buildMap, def, count, Settings.trendHours * GenDate.TicksPerHour, out int change, out int elapsed))
        {
            info.has = true;
            info.change = change;
            info.trend = Significant(change, count - change) ? Math.Sign(change) : 0;

            // At the rate it fell over the trend period.
            if (info.trend < 0 && count > 0 && elapsed > 0)
            {
                info.ticksLeft = (int)Math.Min(int.MaxValue, (long)count * elapsed / -change);
            }
        }

        TrendMemo[def] = info;
        return info;
    }

    private static int MarkOf(ThingDef def)
    {
        return buildMap != null && ResourceDiscoveries.Current is ResourceDiscoveries discoveries ? discoveries.MarkOf(buildMap, def) : ResourceDiscoveries.None;
    }

    private static bool Has(ShowFilter kind)
    {
        return (Settings.filters & kind) != 0;
    }

    // A thing matches the filter when it fits any chosen kind.
    private static bool Matches(ThingDef def)
    {
        if (MatchMemo.TryGetValue(def, out bool matches))
        {
            return matches;
        }

        matches = LowMatch(Keys.Of(def)) || (FamilyOf(def) is ThingCategoryDef family && LowMatch(FamilyKey(family)) && CountOf(def) > 0)
            || (Has(ShowFilter.New) && MarkOf(def) != ResourceDiscoveries.None);
        if (!matches && Has(ShowFilter.Trending | ShowFilter.Falling))
        {
            int trend = TrendOf(def).trend;
            matches = (Has(ShowFilter.Trending) && trend != 0) || (Has(ShowFilter.Falling) && trend < 0);
        }

        MatchMemo[def] = matches;
        return matches;
    }

    // A row key below or near its minimum, when the low stock filter is chosen.
    private static bool LowMatch(string key)
    {
        return Has(ShowFilter.Low) && (LowKeys.Contains(key) || NearKeys.Contains(key));
    }

    private static bool AnyMarked(List<ThingDef> defs)
    {
        foreach (ThingDef def in defs)
        {
            if (MarkOf(def) != ResourceDiscoveries.None && !Hidden.Contains(Keys.Of(def)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GroupMarked(GroupInfo group, int depth)
    {
        if (ByGroup.TryGetValue(group.key, out List<ThingDef>? defs) && AnyMarked(defs))
        {
            return true;
        }

        if (depth < MaxDepth)
        {
            foreach (GroupInfo child in group.children)
            {
                if (!Hidden.Contains(child.rowKey) && GroupMarked(child, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CategoryMarked(ThingCategoryDef category, int depth)
    {
        if (depth > MaxDepth)
        {
            return false;
        }

        foreach (Def child in Children(category))
        {
            if (child is ThingCategoryDef inner)
            {
                if (!Hidden.Contains(Keys.Of(inner)) && CategoryMarked(inner, depth + 1))
                {
                    return true;
                }
            }
            else if (child is ThingDef thing && !Hidden.Contains(Keys.Of(thing)) && MarkOf(thing) != ResourceDiscoveries.None)
            {
                return true;
            }
        }

        return false;
    }

    // While filtering, a category shows when anything inside it matches.
    private static bool CategoryMatches(ThingCategoryDef category, int depth)
    {
        if (LowMatch(Keys.Of(category)))
        {
            return true;
        }

        if (depth > MaxDepth)
        {
            return false;
        }

        foreach (Def child in Children(category))
        {
            if (child is ThingCategoryDef inner)
            {
                if (!Hidden.Contains(Keys.Of(inner)) && CategoryMatches(inner, depth + 1))
                {
                    return true;
                }
            }
            else if (child is ThingDef thing && thing.PlayerAcquirable && !Pinned.Contains(Keys.Of(thing)) && Visible(thing, CountOf(thing)))
            {
                return true;
            }
        }

        return false;
    }

    // ---- Sorting by change ----------------------------------------------------------------

    private static int UnitChange(ReadoutUnit unit)
    {
        if (unit.def != null)
        {
            return Math.Abs(TrendOf(unit.def).change);
        }

        int change = 0;
        foreach (ThingDef member in unit.members)
        {
            change += TrendOf(member).change;
        }

        return Math.Abs(change);
    }

    // Biggest movers first; rows that didn't change keep their order after them.
    private static void SortByChange(List<ReadoutUnit> units)
    {
        if (!sorting || units.Count < 2)
        {
            return;
        }

        List<(ReadoutUnit unit, int change, int index)> keyed = new List<(ReadoutUnit, int, int)>(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            keyed.Add((units[i], UnitChange(units[i]), i));
        }

        keyed.Sort((a, b) => a.change != b.change ? b.change.CompareTo(a.change) : a.index.CompareTo(b.index));
        for (int i = 0; i < keyed.Count; i++)
        {
            units[i] = keyed[i].unit;
        }
    }

    // In the category tree only the things at a level are reordered; categories keep their places.
    private static void SortLevel(List<Def> level)
    {
        if (!sorting)
        {
            return;
        }

        List<int> slots = new List<int>();
        List<(ThingDef def, int change, int index)> things = new List<(ThingDef, int, int)>();
        for (int i = 0; i < level.Count; i++)
        {
            if (level[i] is ThingDef thing)
            {
                slots.Add(i);
                things.Add((thing, Math.Abs(TrendOf(thing).change), things.Count));
            }
        }

        if (things.Count < 2)
        {
            return;
        }

        things.Sort((a, b) => a.change != b.change ? b.change.CompareTo(a.change) : a.index.CompareTo(b.index));
        for (int i = 0; i < slots.Count; i++)
        {
            level[slots[i]] = things[i].def;
        }
    }

    // ---- Run-out estimates ------------------------------------------------------------------

    public static string RunOutShort(int ticks)
    {
        float days = ticks / (float)GenDate.TicksPerDay;
        return days >= 1f ? "RRP_DaysShort".Translate(days.ToString("0.#")) : "RRP_HoursShort".Translate(Mathf.Max(1, ticks / GenDate.TicksPerHour));
    }

    // ---- Hover chart sources ------------------------------------------------------------------

    // The things a group's total is made of, for its hover chart; null when charts are off.
    private static List<ThingDef>? ChartDefsOf(GroupInfo group)
    {
        if (!Settings.hoverChart || !Settings.trends)
        {
            return null;
        }

        List<ThingDef> defs = new List<ThingDef>();
        AddGroupDefs(group, defs, 0);
        return defs;
    }

    private static void AddGroupDefs(GroupInfo group, List<ThingDef> defs, int depth)
    {
        if (ByGroup.TryGetValue(group.key, out List<ThingDef>? own))
        {
            foreach (ThingDef def in own)
            {
                if (ThingInTotal(def))
                {
                    defs.Add(def);
                }
            }
        }

        if (depth < MaxDepth)
        {
            foreach (GroupInfo child in group.children)
            {
                if (InTotal(child.rowKey))
                {
                    AddGroupDefs(child, defs, depth + 1);
                }
            }
        }
    }

    private static List<ThingDef>? ChartDefsOf(ThingCategoryDef category)
    {
        if (!Settings.hoverChart || !Settings.trends)
        {
            return null;
        }

        List<ThingDef> defs = new List<ThingDef>();
        AddCategoryDefs(category, defs, 0);
        return defs;
    }

    private static void AddCategoryDefs(ThingCategoryDef category, List<ThingDef> defs, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        foreach (Def child in Children(category))
        {
            if (child is ThingCategoryDef inner)
            {
                if (InTotal(Keys.Of(inner)))
                {
                    AddCategoryDefs(inner, defs, depth + 1);
                }
            }
            else if (child is ThingDef thing && thing.CountAsResource && InTotal(Keys.Of(thing)))
            {
                defs.Add(thing);
            }
        }
    }
}
