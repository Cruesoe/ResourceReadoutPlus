using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>One drawn line of the readout, built when counts or settings change.</summary>
public sealed class Row
{
    public RowKind kind;
    public string key = "";
    public string label = "";
    public int depth;

    // The thing shown (for a family, the first visible member's icon), or the category behind a category or family row.
    public ThingDef? def;
    public ThingCategoryDef? category;
    public Texture? icon;
    public List<ThingDef>? members;

    public int count;
    public string countText = "";
    public float countWidth = -1f;
    public int minimum = -1;
    public bool low;
    public bool near;

    // Change over the trend window: sign drawn as an arrow, amount for the tooltip.
    public int trend;
    public int change;
    public bool hasChange;

    // Ticks until it runs out at the current rate (-1 when not falling), and its short form when drawn in the row.
    public int ticksLeft = -1;
    public string? runOutText;
    public float runOutWidth = -1f;

    // A thing's new or back-in-stock mark; for headers and families, whether anything inside has one.
    public int mark;
    public bool marked;

    // The things a group's or category's hover chart adds up.
    public List<ThingDef>? chartDefs;

    // Tooltip text, built the first time the row is hovered.
    public string? tipText;

    public bool expandable;
    public bool open;

    // Dragging: the list this row sits in, its key in that list, and the list its children form.
    public bool draggable;
    public string list = "";
    public string unit = "";
    public string? opens;

    public Func<string>? tipTitle;
    public Func<string>? tipBody;
}
