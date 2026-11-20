using Verse;

namespace ResourceReadoutPlus;

/// <summary>A group the player made; items join it by drag or the right-click menu.</summary>
public sealed class CustomGroup : IExposable
{
    public string key = "";
    public string label = "";

    public void ExposeData()
    {
        Scribe_Values.Look(ref key, "key", "");
        Scribe_Values.Look(ref label, "label", "");
    }
}
