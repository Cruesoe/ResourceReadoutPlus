using System.Collections.Generic;
using UnityEngine;

namespace ResourceReadoutPlus;

/// <summary>One group as the grouped mode shows it: a built-in group or one the player made.</summary>
public sealed class GroupInfo
{
    public string key = "";
    public string rowKey = "";
    public string label = "";
    public Texture? icon;
    public bool custom;

    // Nesting, rebuilt with the groups: the parent group (null at the top level), child groups in order, and depth from the top.
    public GroupInfo? parent;
    public readonly List<GroupInfo> children = new List<GroupInfo>();
    public int depth;

    // Drag list key for this group's child groups.
    public string childList = "";
}
