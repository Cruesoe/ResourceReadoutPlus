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
}
