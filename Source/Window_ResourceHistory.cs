using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>A resource's count over the last ten days on the current map, from the hourly trend samples.</summary>
public class Window_ResourceHistory : Window
{
    private readonly ThingDef def;
    private readonly string title;

    public Window_ResourceHistory(ThingDef def)
    {
        this.def = def;
        title = "RRP_HistoryTitle".Translate(def.LabelCap);
        forcePause = false;
        doCloseX = true;
        closeOnClickedOutside = true;
        draggable = true;
        preventCameraMotion = false;
    }

    public override Vector2 InitialSize => new Vector2(520f, 280f);

    public override void DoWindowContents(Rect inRect)
    {
        Map? map = Find.CurrentMap;
        TrendSeries? series = map == null ? null : ResourceTrends.Current?.SeriesFor(map, def);
        Rect card = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 30f);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 30f, 26f), title);
        TrendCard.Draw(card, def.LabelCap, def.uiIcon, Theme.HighlightBlue, Theme.TextBright, series, null, false, "RRP_HistoryEmpty".Translate());
    }
}
