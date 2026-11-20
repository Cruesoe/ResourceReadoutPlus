using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Clicking a thing's row jumps to its stacks one after another: stored stacks first, then the rest of the map for rows counting all.</summary>
public static class ReadoutJump
{
    // Session lifetime: only the last row clicked is remembered.
    private static ThingDef? lastDef;
    private static int lastIndex = -1;
    private static readonly List<Thing> Found = new List<Thing>();

    public static void Forget()
    {
        lastDef = null;
        lastIndex = -1;
        Found.Clear();
    }

    public static void Next(Map map, ThingDef def)
    {
        Collect(map, def);
        if (Found.Count == 0)
        {
            Messages.Message("RRP_NoneToJumpTo".Translate(def.label), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        lastIndex = lastDef == def ? (lastIndex + 1) % Found.Count : 0;
        lastDef = def;
        Thing thing = Found[lastIndex];
        Found.Clear();
        if (thing.Spawned)
        {
            CameraJumper.TryJumpAndSelect(thing);
        }
        else
        {
            CameraJumper.TryJump(thing.PositionHeld, map);
        }
    }

    private static void Collect(Map map, ThingDef def)
    {
        Found.Clear();
        List<SlotGroup> groups = map.haulDestinationManager.AllGroupsListForReading;
        for (int i = 0; i < groups.Count; i++)
        {
            foreach (Thing held in groups[i].HeldThings)
            {
                if (held.GetInnerIfMinified().def == def && !held.PositionHeld.Fogged(map))
                {
                    Found.Add(held);
                }
            }
        }

        if (!ReadoutCounts.IsCountedAll(def))
        {
            return;
        }

        List<Thing> things = map.listerThings.ThingsOfDef(def);
        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            if (thing.def.category != ThingCategory.Building && !thing.IsInAnyStorage() && !thing.Position.Fogged(map))
            {
                Found.Add(thing);
            }
        }
    }
}
