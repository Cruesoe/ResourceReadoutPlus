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
            case RowKind.Header:
                CoreWidgets.Line(new Rect(inner.x + 4f, inner.y, inner.width - 4f, inner.height), row.label, Theme.TextDim);
                break;
            case RowKind.Group:
            case RowKind.Category:
                DrawHeader(row, inner);
                break;
            default:
                DrawThing(row, inner, index);
                break;
        }

        GUI.color = Color.white;
        if (!dragging && interactive && row.tipBody != null)
        {
            CoreWidgets.LazyTip(rect, row.tipTitle, row.tipBody);
        }
    }

    private static void DrawHeader(Row row, Rect rect)
    {
        CoreWidgets.GroupRow(rect, !row.open);
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
        if (Settings.showNames)
        {
            float nameLeft = icon.xMax + 6f;
            Theme.LabelTruncated(new Rect(nameLeft, rect.y, countLeft - 4f - nameLeft, rect.height), row.label, TextAnchor.MiddleLeft, Theme.TextBright);
        }
    }

    private static void DrawThing(Row row, Rect rect, int index)
    {
        CoreWidgets.ListRow(rect, false, index % 2 == 1, Theme.HighlightGray);
        if (row.low)
        {
            Rounded.Fill(rect.ContractedBy(1f), Faces.Fade(LowColor, 0.18f));
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

        float countRight = rect.xMax - 4f - (Settings.trends ? ArrowWidth : 0f);
        if (Settings.showNames)
        {
            DrawCount(row, rect, countRight, out float countLeft);
            float nameLeft = icon.xMax + 5f;
            Theme.LabelTruncated(new Rect(nameLeft, rect.y, countLeft - 4f - nameLeft, rect.height), row.label, TextAnchor.MiddleLeft, Theme.TextBright);
        }
        else
        {
            CoreWidgets.Line(new Rect(icon.xMax + 6f, rect.y, countRight - icon.xMax - 6f, rect.height), row.countText, row.low ? LowColor : Theme.TextBright);
        }

        if (row.trend != 0)
        {
            Rect marker = new Rect(rect.xMax - 4f - ArrowWidth + 1f, Mathf.Round(rect.center.y - 4f), 8f, 8f);
            CoreWidgets.TrendArrow(marker, row.trend > 0, row.trend > 0 ? RisingColor : FallingColor);
        }
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
