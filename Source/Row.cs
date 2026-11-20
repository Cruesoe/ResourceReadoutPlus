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

    // Change over the trend window: sign drawn as an arrow, amount for the tooltip.
    public int trend;
    public int change;
    public bool hasChange;

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
