using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>A row's tooltip: its text and, from the hourly trend samples, a small chart of the last few days.</summary>
public sealed class ResourceTipCard : ICardContent
{
    private const float MinWidth = 240f;
    private const float MaxWidth = 300f;
    private const float Gap = 8f;
    private const float CaptionHeight = 20f;
    private const float ChartHeight = 64f;

    private static readonly ResourceTipCard Instance = new ResourceTipCard();

    private Row? row;

    // The width for the last row measured.
    private Row? widthRow;
    private float width;

    // The chart's points for the hovered row; window lifetime of the tooltip, rebuilt when the row, its build or the samples change.
    private Row? chartRow;
    private int chartTrends = -1;
    private int chartDays = -1;
    private Map? chartMap;
    private readonly List<int> ticks = new List<int>();
    private readonly List<int> values = new List<int>();
    private readonly SortedDictionary<int, int> sums = new SortedDictionary<int, int>();
    private readonly List<Vector2> points = new List<Vector2>();
    private int low;
    private int high;
    private string caption = "";
    private string range = "";

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public static void Forget()
    {
        Instance.row = null;
        Instance.widthRow = null;
        Instance.chartRow = null;
        Instance.chartMap = null;
    }

    public static void Region(Rect rect, Row row)
    {
        if (Event.current.type != EventType.Repaint || !Mouse.IsOver(rect) || CoreWidgets.HoldTips)
        {
            return;
        }

        row.tipText ??= row.tipBody?.Invoke() ?? "";
        Instance.row = row;
        HoverCard.ContentRegion(rect, row.key.GetHashCode(), row.label, Instance, row.tipText);
    }

    public float Width
    {
        get
        {
            if (row?.tipText == null)
            {
                return MinWidth;
            }

            if (widthRow == row)
            {
                return width;
            }

            widthRow = row;
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;
            Text.Font = GameFont.Small;
            float widest = 0f;
            foreach (string line in row.tipText.Split('\n'))
            {
                widest = Mathf.Max(widest, Text.CalcSize(line).x);
            }

            Text.WordWrap = wrap;
            width = Mathf.Clamp(Mathf.Ceil(widest) + 2f, MinWidth, MaxWidth);
            return width;
        }
    }

    public float Height(float width)
    {
        float height = TextHeight(width);
        if (EnsureChart())
        {
            height += Gap + CaptionHeight + ChartHeight;
        }

        return height;
    }

    private float TextHeight(float width)
    {
        if (row?.tipText.NullOrEmpty() != false)
        {
            return 0f;
        }

        bool wrap = Text.WordWrap;
        Text.WordWrap = true;
        Text.Font = GameFont.Small;
        float height = Text.CalcHeight(row!.tipText, width);
        Text.WordWrap = wrap;
        return height;
    }

    public void Draw(Rect rect, float alpha, float age)
    {
        if (row == null)
        {
            return;
        }

        float y = rect.y;
        float text = TextHeight(rect.width);
        if (text > 0f)
        {
            bool wrap = Text.WordWrap;
            Text.WordWrap = true;
            Theme.Label(new Rect(rect.x, y, rect.width, text), row.tipText, TextAnchor.UpperLeft, CardParts.Fade(Theme.TextBright, alpha));
            Text.WordWrap = wrap;
            y += text;
        }

        if (!EnsureChart())
        {
            return;
        }

        y += Gap;
        Rect captionRect = new Rect(rect.x, y, rect.width, CaptionHeight);
        Theme.Label(captionRect, caption, TextAnchor.MiddleLeft, CardParts.Fade(Theme.TextDim, alpha));
        Theme.Label(captionRect, range, TextAnchor.MiddleRight, CardParts.Fade(Theme.TextDim, alpha));
        Rect plot = new Rect(rect.x, captionRect.yMax, rect.width, ChartHeight);
        DrawChart(plot, alpha);
    }

    private void DrawChart(Rect plot, float alpha)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        Rounded.Fill(plot, CardParts.Fade(Theme.BGDark, alpha));
        Rect inside = plot.ContractedBy(5f);
        if (row!.minimum >= low && row.minimum <= high)
        {
            float y = Mathf.Round(Y(inside, row.minimum));
            Theme.Fill(new Rect(inside.x, y, inside.width, 1f), CardParts.Fade(Theme.ModColor(ReadoutPanel.PackageId, "Low"), 0.7f * alpha));
        }

        int first = ticks[0];
        int last = ticks[ticks.Count - 1];
        points.Clear();
        for (int i = 0; i < ticks.Count; i++)
        {
            float x = last > first ? Mathf.Lerp(inside.x, inside.xMax, Mathf.InverseLerp(first, last, ticks[i])) : inside.xMax;
            points.Add(new Vector2(x, Y(inside, values[i])));
        }

        Color line = CardParts.Fade(Color.Lerp(Theme.HighlightBlue, Color.white, 0.2f), alpha);
        for (int i = 1; i < points.Count; i++)
        {
            Beams.Stretch(points[i - 1], points[i], line, line, 2f);
        }

        Rounded.Circle(points[points.Count - 1], 3f, line);
    }

    private float Y(Rect rect, int value)
    {
        return Mathf.Lerp(rect.yMax, rect.y, Mathf.InverseLerp(low, high, value));
    }

    // True when there are at least two samples in the chart's window.
    private bool EnsureChart()
    {
        Map? map = Find.CurrentMap;
        if (row == null || map == null || !Settings.hoverChart || !Settings.trends || ResourceTrends.Current is not ResourceTrends trends)
        {
            return false;
        }

        if (chartRow != row || chartTrends != ResourceTrends.Version || chartDays != Settings.chartDays || chartMap != map)
        {
            chartRow = row;
            chartTrends = ResourceTrends.Version;
            chartDays = Settings.chartDays;
            chartMap = map;
            Collect(trends, map);
        }

        return ticks.Count >= 2;
    }

    private void Collect(ResourceTrends trends, Map map)
    {
        ticks.Clear();
        values.Clear();
        sums.Clear();
        int start = Find.TickManager.TicksGame - chartDays * GenDate.TicksPerDay;
        if (row!.kind == RowKind.Item && row.def != null)
        {
            Add(trends.SeriesFor(map, row.def), start);
        }
        else if ((row.members ?? row.chartDefs) is List<ThingDef> defs)
        {
            foreach (ThingDef def in defs)
            {
                Add(trends.SeriesFor(map, def), start);
            }
        }

        foreach (KeyValuePair<int, int> entry in sums)
        {
            ticks.Add(entry.Key);
            values.Add(entry.Value);
        }

        if (ticks.Count < 2)
        {
            return;
        }

        low = int.MaxValue;
        high = int.MinValue;
        foreach (int value in values)
        {
            low = Mathf.Min(low, value);
            high = Mathf.Max(high, value);
        }

        caption = "RRP_ChartCaption".Translate(chartDays);
        range = "RRP_ChartRange".Translate(ReadoutData.Format(low), ReadoutData.Format(high));
        if (low == high)
        {
            low--;
            high++;
        }
    }

    // Samples are taken for every resource at the same ticks, so totals add up tick by tick.
    private void Add(TrendSeries? series, int start)
    {
        if (series == null)
        {
            return;
        }

        for (int i = 0; i < series.Count; i++)
        {
            int tick = series.ticks[i];
            if (tick < start)
            {
                continue;
            }

            sums[tick] = (sums.TryGetValue(tick, out int sum) ? sum : 0) + series.values[i];
        }
    }
}
