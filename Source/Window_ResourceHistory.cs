using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>A resource's count over the last ten days on the current map, from the hourly trend samples.</summary>
public class Window_ResourceHistory : ThemedPopup
{
    private readonly ThingDef def;
    private readonly string title;

    // The series shown; window lifetime, looked up again when the map or the samples change.
    private TrendSeries? series;
    private Map? seriesMap;
    private int seriesFor = -1;

    public Window_ResourceHistory(ThingDef def)
    {
        this.def = def;
        title = "RRP_HistoryTitle".Translate(def.LabelCap);
        AsDialog();
        forcePause = false;
        absorbInputAroundWindow = false;
    }

    public override Vector2 InitialSize => new Vector2(520f, 300f);

    protected override string Part => "Resource Readout+ history";

    protected override void Contents(Rect inRect)
    {
        Rect body = TitleBar(inRect, def.uiIcon, title, null, null);
        Map? map = Find.CurrentMap;
        if (map != seriesMap || seriesFor != ResourceTrends.Version)
        {
            seriesMap = map;
            seriesFor = ResourceTrends.Version;
            series = map == null ? null : ResourceTrends.Current?.SeriesFor(map, def);
        }

        TrendCard.Draw(body, def.LabelCap, def.uiIcon, Theme.HighlightBlue, Theme.TextBright, series, null, false, "RRP_HistoryEmpty".Translate());
    }
}
