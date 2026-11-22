using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Hourly counts of every resource on each player home map, saved with the game; drives trend arrows and the history window.</summary>
public class ResourceTrends : TrendBook
{
    private const string Series = "count";

    public static ResourceTrends? Current;

    // Bumped after each sample; the readout rebuilds its arrows.
    public static int Version;

    // Maps sampled so far, so a removed map's records can be dropped.
    private List<int> mapIds = new List<int>();

    public ResourceTrends(Game game)
    {
        Current = this;
    }

    protected override int SampleEvery => GenDate.TicksPerHour;

    // Ten days of hourly samples.
    protected override int MostSamples => GenDate.HoursPerDay * 10;

    protected override string Owner => "Resource Readout+";

    public static string Id(Map map, ThingDef def)
    {
        return map.uniqueID + "/" + def.defName;
    }

    public TrendSeries? SeriesFor(Map map, ThingDef def)
    {
        return SeriesOf(Id(map, def), Series);
    }

    // Change from the sample at the start of the window to now, and the ticks between them; false without enough history.
    public bool Change(Map map, ThingDef def, int current, int window, out int change, out int elapsed)
    {
        change = 0;
        elapsed = 0;
        TrendSeries? series = SeriesFor(map, def);
        if (series == null || series.Count == 0)
        {
            return false;
        }

        int ticks = Find.TickManager.TicksGame;
        int start = ticks - window;
        if (series.ticks[0] > start + window / 2)
        {
            return false;
        }

        for (int i = 0; i < series.Count; i++)
        {
            if (series.ticks[i] >= start)
            {
                change = current - series.values[i];
                elapsed = ticks - series.ticks[i];
                return true;
            }
        }

        change = current - series.Last;
        elapsed = ticks - series.LastTick;
        return true;
    }

    protected override void Sample(int now)
    {
        if (!ReadoutMod.Settings.trends)
        {
            return;
        }

        List<Map> maps = Find.Maps;
        HashSet<int> present = new HashSet<int>();
        for (int i = 0; i < maps.Count; i++)
        {
            Map map = maps[i];
            present.Add(map.uniqueID);
            if (!map.IsPlayerHome)
            {
                continue;
            }

            if (!mapIds.Contains(map.uniqueID))
            {
                mapIds.Add(map.uniqueID);
            }

            foreach (ThingDef def in map.resourceCounter.AllCountedAmounts.Keys)
            {
                int count = ReadoutCounts.CountFresh(map, def);
                string id = Id(map, def);
                if (count > 0 || Known(id))
                {
                    Add(id, Series, now, count);
                }
            }
        }

        // Records of maps that are gone are dropped.
        for (int i = mapIds.Count - 1; i >= 0; i--)
        {
            if (present.Contains(mapIds[i]))
            {
                continue;
            }

            string prefix = mapIds[i] + "/";
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.CountAsResource)
                {
                    Forget(prefix + def.defName);
                }
            }

            mapIds.RemoveAt(i);
        }

        Version++;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref mapIds, "mapIds", LookMode.Value);
        mapIds ??= new List<int>();
    }
}
