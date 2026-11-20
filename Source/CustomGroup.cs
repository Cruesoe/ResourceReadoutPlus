using Verse;

namespace ResourceReadoutPlus;

/// <summary>A group the player made; items join it by drag or the right-click menu.</summary>
public sealed class CustomGroup : IExposable
{
    public string key = "";
    public string label = "";

    // The group it sits in; empty for the top level.
    public string parent = "";

    public void ExposeData()
    {
        Scribe_Values.Look(ref key, "key", "");
        Scribe_Values.Look(ref label, "label", "");
        Scribe_Values.Look(ref parent, "parent", "");
    }
}
