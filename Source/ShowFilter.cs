using System;

namespace ResourceReadoutPlus;

/// <summary>What the readout's filter keeps; rows matching any chosen kind show.</summary>
[Flags]
public enum ShowFilter
{
    None = 0,
    Trending = 1,
    Falling = 2,
    Low = 4,
    New = 8,
}
