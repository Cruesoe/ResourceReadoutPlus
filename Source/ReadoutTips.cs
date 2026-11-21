using System.Text;
using RimWorld;
using UIRethemed.Core;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Row tooltip text, built only when a tooltip shows.</summary>
public static class ReadoutTips
{
    private static readonly StringBuilder Text = new StringBuilder();

    public static string Body(Row row)
    {
        Map? map = Find.CurrentMap;
        Text.Clear();
        switch (row.kind)
        {
            case RowKind.Item:
                ItemBody(row, map);
                break;
            case RowKind.Family:
                Line("RRP_TipFamily".Translate(row.members?.Count ?? 0));
                Line("RRP_TipTotal".Translate(row.countText));
                break;
            case RowKind.Group:
            case RowKind.Category:
                Line("RRP_TipTotal".Translate(row.countText));
                break;
        }

        if (row.minimum >= 0)
        {
            Line((row.low ? "RRP_TipBelowMinimum" : row.near ? "RRP_TipNearMinimum" : "RRP_TipMinimum").Translate(row.minimum.ToStringCached()));
        }

        if (row.marked && row.kind != RowKind.Item)
        {
            Line("RRP_TipNewInside".Translate());
        }

        Text.AppendLine();
        Text.Append(row.kind == RowKind.Item ? "RRP_TipItemHelp".Translate() : row.expandable ? "RRP_TipGroupHelp".Translate() : "RRP_TipMenuHelp".Translate());
        return Text.ToString();
    }

    private static void ItemBody(Row row, Map? map)
    {
        ThingDef def = row.def!;
        if (!def.description.NullOrEmpty())
        {
            Text.AppendLine(HoverCard.FirstParagraph(def.description.CapitalizeFirst()));
            Text.AppendLine();
        }

        bool all = ReadoutCounts.IsCountedAll(def);
        Line((all ? "RRP_TipOnMap" : "RRP_TipInStorage").Translate(row.count.ToStringCached()));
        if (map != null && !all)
        {
            int outside = ReadoutCounts.OutsideStorage(map, def);
            if (outside > 0)
            {
                Line("RRP_TipOutsideStorage".Translate(outside.ToStringCached()));
            }
        }

        if (row.count > 0)
        {
            Line("RRP_TipValue".Translate((row.count * def.BaseMarketValue).ToStringMoney()));
        }

        if (row.hasChange)
        {
            Line("RRP_TipChange".Translate(row.change.ToStringWithSign(), ReadoutMod.Settings.trendHours.ToStringCached()));
        }

        if (ReadoutMod.Settings.runOutTip && row.ticksLeft >= 0)
        {
            Line("RRP_TipRunOut".Translate(ReadoutData.RunOutShort(row.ticksLeft)));
        }

        if (row.mark != ResourceDiscoveries.None)
        {
            Line((row.mark == ResourceDiscoveries.New ? "RRP_TipNew" : "RRP_TipBack").Translate());
        }
    }

    private static void Line(string text)
    {
        Text.AppendLine(text);
    }
}
