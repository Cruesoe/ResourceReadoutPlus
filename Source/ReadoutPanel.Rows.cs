using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

// One row: group and category headers, things, families and the pinned caption.
public static partial class ReadoutPanel
{
    private const float Indent = 10f;
    private const float CaretWidth = 12f;
    private const float IconSize = 20f;
    private const float ArrowWidth = 10f;

    private static Color LowColor => Theme.ModColor(PackageId, "Low");
    private static Color RisingColor => Theme.ModColor(PackageId, "Rising");
    private static Color FallingColor => Theme.ModColor(PackageId, "Falling");
    private static Color NewColor => Theme.ModColor(PackageId, "New");

    private static readonly Texture2D NewTex = CoreWidgets.Icon("sparkle");
    private static readonly Texture2D BackTex = CoreWidgets.Icon("arrow-counter-clockwise");

    private static void DrawRow(Row row, Rect rect, int index)
    {
        bool faded = dragging && pressRow != null && row.key == pressRow.key;
        if (faded)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
        }

        Rect inner = rect;
        inner.xMin += row.depth * Indent;
        switch (row.kind)
        {
            case RowKind.Group:
            case RowKind.Category:
                DrawHeader(row, inner);
                break;
            default:
                DrawThing(row, inner, index);
                break;
        }

        GUI.color = Color.white;
        if (row.pinnedEnd)
        {
            Theme.Divider(rect.x + 4f, rect.yMax - 1f, rect.width - 8f);
        }

        if (!dragging && interactive && row.tipBody != null)
        {
            if (Settings.hoverChart && Settings.trends)
            {
                ResourceTipCard.Region(rect, row);
            }
            else
            {
                CoreWidgets.LazyTip(rect, row.tipTitle, row.tipBody);
            }
        }
    }

    private const int HeaderSalt = 52611;
    private const int ThingSalt = 52612;

    // Header backgrounds follow the panel's opacity setting.
    private static void DrawHeader(Row row, Rect rect)
    {
        float opacity = Opacity;
        float hover = ThemeMotion.Hover(rect, HeaderSalt, interactive && Mouse.IsOver(rect));
        Rounded.Fill(rect, Faces.Fade(Theme.BG, opacity));
        Rounded.Border(rect, Faces.Fade(Color.Lerp(Color.Lerp(Theme.BGDark, Theme.HighlightGray, 0.35f), Theme.HighlightGray, hover * 0.5f), opacity));
        if (hover > 0f)
        {
            Rounded.Fill(rect.ContractedBy(2f), new Color(1f, 1f, 1f, 0.06f * hover));
        }

        CoreWidgets.DrawIcon(new Rect(rect.x + 6f, rect.y, 12f, rect.height), row.open ? CoreWidgets.ExpandedTex : CoreWidgets.CollapsedTex, Color.Lerp(Theme.TextFaint, Theme.TextBright, hover), 12f);
        if (row.low)
        {
            Rounded.Fill(rect.ContractedBy(1f), Faces.Fade(LowColor, 0.18f));
        }

        Rect icon = new Rect(rect.x + 20f, Mathf.Round(rect.center.y - 8f), 16f, 16f);
        if (row.icon != null)
        {
            if (row.kind == RowKind.Group)
            {
                CoreWidgets.DrawIcon(icon, row.icon, Theme.TextDim);
            }
            else
            {
                GUI.DrawTexture(icon, row.icon);
            }
        }

        float countRight = rect.xMax - 4f - (Settings.trends ? ArrowWidth : 0f);
        DrawCount(row, rect, countRight, out float countLeft);
        countLeft = DrawExtras(row, rect, countLeft);
        if (Settings.showNames)
        {
            float nameLeft = icon.xMax + 6f;
            Theme.LabelTruncated(new Rect(nameLeft, rect.y, countLeft - 4f - nameLeft, rect.height), row.label, TextAnchor.MiddleLeft, Theme.TextBright);
        }
    }

    private static void DrawThing(Row row, Rect rect, int index)
    {
        float opacity = Opacity;
        float hover = ThemeMotion.Hover(rect, ThingSalt, interactive && Mouse.IsOver(rect));
        Color fill = index % 2 == 1 ? Color.Lerp(Theme.BG, Theme.BGDark, 0.5f) : Theme.BG;
        Rounded.Fill(rect, Faces.Fade(fill, opacity));
        if (hover > 0f)
        {
            Rounded.Fill(rect, new Color(1f, 1f, 1f, 0.05f * hover));
        }

        if (row.low)
        {
            Rounded.Fill(rect.ContractedBy(1f), Faces.Fade(LowColor, 0.18f));
        }
        else if (row.mark != ResourceDiscoveries.None)
        {
            Rounded.Fill(rect.ContractedBy(1f), Faces.Fade(NewColor, 0.16f));
        }

        if (row.kind == RowKind.Family)
        {
            Rect caret = new Rect(rect.x + 2f, rect.y, CaretWidth, rect.height);
            CoreWidgets.DrawIcon(caret, row.open ? CoreWidgets.ExpandedTex : CoreWidgets.CollapsedTex, Theme.TextDim, 10f);
        }

        Rect icon = new Rect(rect.x + 2f + CaretWidth, Mathf.Round(rect.center.y - IconSize / 2f), IconSize, IconSize);
        if (row.def != null)
        {
            Widgets.ThingIcon(icon, row.def);
            GUI.color = Color.white;
        }

        if (row.mark != ResourceDiscoveries.None)
        {
            Rect badge = new Rect(icon.xMax - 8f, icon.y - 3f, 12f, 12f);
            CoreWidgets.DrawIcon(badge, row.mark == ResourceDiscoveries.New ? NewTex : BackTex, NewColor, 12f);
        }

        float countRight = rect.xMax - 4f - (Settings.trends ? ArrowWidth : 0f);
        DrawCount(row, rect, countRight, out float countLeft);
        countLeft = DrawExtras(row, rect, countLeft);
        if (Settings.showNames)
        {
            float nameLeft = icon.xMax + 5f;
            Theme.LabelTruncated(new Rect(nameLeft, rect.y, countLeft - 4f - nameLeft, rect.height), row.label, TextAnchor.MiddleLeft, Theme.TextBright);
        }

        if (row.trend != 0)
        {
            Rect marker = new Rect(rect.xMax - 4f - ArrowWidth + 1f, Mathf.Round(rect.center.y - 4f), 8f, 8f);
            CoreWidgets.TrendArrow(marker, row.trend > 0, row.trend > 0 ? RisingColor : FallingColor);
        }
    }

    // Left of the count: the run-out estimate, and a dot when something inside a header or family is new. Returns the new left edge.
    private static float DrawExtras(Row row, Rect rect, float left)
    {
        if (row.runOutText != null)
        {
            if (row.runOutWidth < 0f)
            {
                row.runOutWidth = CoreWidgets.Width(row.runOutText) + 2f;
            }

            left -= row.runOutWidth + 4f;
            CoreWidgets.Line(new Rect(left, rect.y, row.runOutWidth, rect.height), row.runOutText, FallingColor, GameFont.Small, TextAnchor.MiddleRight);
        }

        if (row.marked && row.kind != RowKind.Item)
        {
            left -= 10f;
            Rounded.Circle(new Vector2(left + 3f, rect.center.y), 3f, NewColor);
        }

        return left;
    }

    // Draws the count right-aligned at countRight; countLeft is where it starts.
    private static void DrawCount(Row row, Rect rect, float countRight, out float countLeft)
    {
        if (row.countWidth < 0f)
        {
            row.countWidth = CoreWidgets.Width(row.countText) + 2f;
        }

        countLeft = countRight - row.countWidth;
        CoreWidgets.Line(new Rect(countLeft, rect.y, row.countWidth, rect.height), row.countText, row.low ? LowColor : Theme.TextBright, GameFont.Small, TextAnchor.MiddleRight);
    }
}
