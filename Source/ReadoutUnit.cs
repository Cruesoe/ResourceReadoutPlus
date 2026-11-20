using System.Collections.Generic;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>One movable entry of a list while it is built: a thing, or a family of look-alike things.</summary>
public sealed class ReadoutUnit
{
    public string key = "";
    public ThingDef? def;
    public ThingCategoryDef? family;
    public readonly List<ThingDef> members = new List<ThingDef>();
    public int index;

    // Display text for the organizer window, filled when its list is built.
    public string label = "";
    public string note = "";
}
