using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Counts as the readout shows them: the base game's stored count, or everything on the map for rows set to count all.</summary>
public static class ReadoutCounts
{
    // Bumped after every base-game count update, the only time stored counts change.
    public static int Stamp;

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    // Things set to count all, rebuilt when settings change.
    private static readonly HashSet<ThingDef> CountAllDefs = new HashSet<ThingDef>();
    private static int countAllFor = -1;

    // Map-wide totals for the current map's count-all things; game lifetime, cleared by Forget.
    private static readonly Dictionary<ThingDef, int> MapCounts = new Dictionary<ThingDef, int>();
    private static Map? countedMap;
    private static int countedStamp = -1;
    private static int countedSettings = -1;

    public static void CountsUpdated()
    {
        Stamp++;
    }

    public static void Forget()
    {
        MapCounts.Clear();
        countedMap = null;
        countedStamp = -1;
    }

    public static bool IsCountedAll(ThingDef def)
    {
        EnsureCountAll();
        return CountAllDefs.Contains(def);
    }

    public static int Count(Map map, ThingDef def)
    {
        if (!IsCountedAll(def))
        {
            return map.resourceCounter.GetCount(def);
        }

        EnsureMapCounts(map);
        return MapCounts.TryGetValue(def, out int count) ? count : 0;
    }

    // Any map, uncached; for trend samples.
    public static int CountFresh(Map map, ThingDef def)
    {
        return IsCountedAll(def) ? CountOnMap(map, def) : map.resourceCounter.GetCount(def);
    }

    // Spawned things of this kind lying outside storage, as the stored count would count them.
    public static int OutsideStorage(Map map, ThingDef def)
    {
        int total = 0;
        List<Thing> things = map.listerThings.ThingsOfDef(def);
        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            if (thing.def.category != ThingCategory.Building && !thing.IsInAnyStorage() && ShouldCount(thing, thing, map))
            {
                total += thing.stackCount;
            }
        }

        return total;
    }

    private static void EnsureCountAll()
    {
        if (countAllFor == Settings.version)
        {
            return;
        }

        countAllFor = Settings.version;
        CountAllDefs.Clear();
        foreach (string key in Settings.countAll)
        {
            if (Keys.ThingOf(key) is ThingDef thing)
            {
                if (thing.CountAsResource)
                {
                    CountAllDefs.Add(thing);
                }
            }
            else if (Keys.CategoryOf(key) is ThingCategoryDef category)
            {
                foreach (ThingDef inner in category.DescendantThingDefs)
                {
                    if (inner.CountAsResource)
                    {
                        CountAllDefs.Add(inner);
                    }
                }
            }
            else if (Keys.GroupOf(key) is string group)
            {
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (def.CountAsResource && ReadoutGroups.GroupOf(def) == group)
                    {
                        CountAllDefs.Add(def);
                    }
                }
            }
        }
    }

    private static void EnsureMapCounts(Map map)
    {
        if (countedMap == map && countedStamp == Stamp && countedSettings == Settings.version)
        {
            return;
        }

        countedMap = map;
        countedStamp = Stamp;
        countedSettings = Settings.version;
        MapCounts.Clear();
        foreach (ThingDef def in CountAllDefs)
        {
            MapCounts[def] = CountOnMap(map, def);
        }
    }

    private static int CountOnMap(Map map, ThingDef def)
    {
        int total = 0;
        List<Thing> things = map.listerThings.ThingsOfDef(def);
        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            // Installed furniture shares its def with the minified item but is not stock.
            if (thing.def.category != ThingCategory.Building && ShouldCount(thing, thing, map))
            {
                total += thing.stackCount;
            }
        }

        List<Thing> minified = map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing);
        for (int i = 0; i < minified.Count; i++)
        {
            Thing inner = minified[i].GetInnerIfMinified();
            if (inner != minified[i] && inner.def == def && ShouldCount(minified[i], inner, map))
            {
                total += inner.stackCount;
            }
        }

        // Things held inside storage buildings, which the listers above miss.
        List<SlotGroup> groups = map.haulDestinationManager.AllGroupsListForReading;
        for (int i = 0; i < groups.Count; i++)
        {
            foreach (Thing held in groups[i].HeldThings)
            {
                if (held.Spawned)
                {
                    continue;
                }

                Thing inner = held.GetInnerIfMinified();
                if (inner.def == def && !inner.IsNotFresh())
                {
                    total += inner.stackCount;
                }
            }
        }

        if (Settings.countCarried)
        {
            total += Carried(map, def);
        }

        return total;
    }

    // Carried by colonists on the map or in their inventories.
    private static int Carried(Map map, ThingDef def)
    {
        int total = 0;
        List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
        for (int i = 0; i < colonists.Count; i++)
        {
            Pawn pawn = colonists[i];
            Thing? carried = pawn.carryTracker?.CarriedThing;
            if (carried != null && carried.GetInnerIfMinified().def == def)
            {
                total += carried.stackCount;
            }

            ThingOwner? inventory = pawn.inventory?.innerContainer;
            if (inventory == null)
            {
                continue;
            }

            for (int j = 0; j < inventory.Count; j++)
            {
                Thing item = inventory[j];
                if (item.GetInnerIfMinified().def == def)
                {
                    total += item.stackCount;
                }
            }
        }

        return total;
    }

    // The base game's rule for stored things: no rotten food and nothing under fog.
    private static bool ShouldCount(Thing outer, Thing inner, Map map)
    {
        return !inner.IsNotFresh() && !outer.Position.Fogged(map);
    }
}
