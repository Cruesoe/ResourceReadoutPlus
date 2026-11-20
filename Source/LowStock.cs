namespace ResourceReadoutPlus;

/// <summary>A row below its minimum stock, for the low-stock alert.</summary>
public sealed class LowStock
{
    public string label = "";
    public int count;
    public int minimum;
}
