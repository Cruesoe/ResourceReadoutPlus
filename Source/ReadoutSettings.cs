using System.Collections.Generic;
using Verse;

namespace ResourceReadoutPlus;

public class ReadoutSettings : ModSettings
{
    public const float MinWidth = 150f;
    public const float MaxWidth = 340f;
    public const float UsualWidth = 210f;
    public const int MinTrendHours = 6;
    public const int MaxTrendHours = 120;
    public const int UsualTrendHours = 24;

    // Display
    public ReadoutMode mode = ReadoutMode.Grouped;
    public bool showNames = true;
    public float width = UsualWidth;
    public bool shortNumbers;
    public bool showZero;
    public bool folded;

    // Counting
    public bool countHiddenInTotals = true;
    public bool countCarried;

    // Trends and minimum stock
    public bool trends = true;
    public int trendHours = UsualTrendHours;
    public bool lowStockAlert = true;

    // Order per mode: saved keys ranked in order; unranked rows follow in the base game's order.
    public List<string> orderGrouped = new List<string>();
    public List<string> orderSimple = new List<string>();
    public List<string> orderCategorized = new List<string>();

    // Moves: thing key to group key (grouped), row key to category key or TOP (categorized).
    public Dictionary<string, string> groupOf = new Dictionary<string, string>();
    public Dictionary<string, string> parentOf = new Dictionary<string, string>();

    // Groups
    public List<string> groupOrder = new List<string>();
    public List<CustomGroup> customGroups = new List<CustomGroup>();
    public Dictionary<string, string> groupLabels = new Dictionary<string, string>();
    public int nextGroupId = 1;

    // Per row: hidden, counted map-wide, pinned to the top, minimum stock.
    public List<string> hidden = new List<string>();
    public List<string> countAll = new List<string>();
    public List<string> pinned = new List<string>();
    public Dictionary<string, int> minimums = new Dictionary<string, int>();

    // Categories whose things show as one combined row.
    public List<string> families = new List<string>();
    public bool familiesSet;

    // Open state: groups start open, categories and families closed.
    public List<string> foldedGroups = new List<string>();
    public List<string> openCategories = new List<string>();
    public List<string> openFamilies = new List<string>();

    public bool importedUiPlus;

    // Bumped by every change; caches rebuild on the next read.
    public int version;

    public void Changed()
    {
        version++;
        ReadoutMod.Instance?.WriteSettings();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref mode, "mode", ReadoutMode.Grouped);
        Scribe_Values.Look(ref showNames, "showNames", true);
        Scribe_Values.Look(ref width, "width", UsualWidth);
        Scribe_Values.Look(ref shortNumbers, "shortNumbers", false);
        Scribe_Values.Look(ref showZero, "showZero", false);
        Scribe_Values.Look(ref folded, "folded", false);
        Scribe_Values.Look(ref countHiddenInTotals, "countHiddenInTotals", true);
        Scribe_Values.Look(ref countCarried, "countCarried", false);
        Scribe_Values.Look(ref trends, "trends", true);
        Scribe_Values.Look(ref trendHours, "trendHours", UsualTrendHours);
        Scribe_Values.Look(ref lowStockAlert, "lowStockAlert", true);

        Scribe_Collections.Look(ref orderGrouped, "orderGrouped", LookMode.Value);
        Scribe_Collections.Look(ref orderSimple, "orderSimple", LookMode.Value);
        Scribe_Collections.Look(ref orderCategorized, "orderCategorized", LookMode.Value);
        Scribe_Collections.Look(ref groupOf, "groupOf", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref parentOf, "parentOf", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref groupOrder, "groupOrder", LookMode.Value);
        Scribe_Collections.Look(ref customGroups, "customGroups", LookMode.Deep);
        Scribe_Collections.Look(ref groupLabels, "groupLabels", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref nextGroupId, "nextGroupId", 1);
        Scribe_Collections.Look(ref hidden, "hidden", LookMode.Value);
        Scribe_Collections.Look(ref countAll, "countAll", LookMode.Value);
        Scribe_Collections.Look(ref pinned, "pinned", LookMode.Value);
        Scribe_Collections.Look(ref minimums, "minimums", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref families, "families", LookMode.Value);
        Scribe_Values.Look(ref familiesSet, "familiesSet", false);
        Scribe_Collections.Look(ref foldedGroups, "foldedGroups", LookMode.Value);
        Scribe_Collections.Look(ref openCategories, "openCategories", LookMode.Value);
        Scribe_Collections.Look(ref openFamilies, "openFamilies", LookMode.Value);
        Scribe_Values.Look(ref importedUiPlus, "importedUiPlus", false);

        orderGrouped ??= new List<string>();
        orderSimple ??= new List<string>();
        orderCategorized ??= new List<string>();
        groupOf ??= new Dictionary<string, string>();
        parentOf ??= new Dictionary<string, string>();
        groupOrder ??= new List<string>();
        customGroups ??= new List<CustomGroup>();
        groupLabels ??= new Dictionary<string, string>();
        hidden ??= new List<string>();
        countAll ??= new List<string>();
        pinned ??= new List<string>();
        minimums ??= new Dictionary<string, int>();
        families ??= new List<string>();
        foldedGroups ??= new List<string>();
        openCategories ??= new List<string>();
        openFamilies ??= new List<string>();
        version++;
    }

    public List<string> OrderFor(ReadoutMode readoutMode)
    {
        return readoutMode switch
        {
            ReadoutMode.Simple => orderSimple,
            ReadoutMode.Categorized => orderCategorized,
            _ => orderGrouped,
        };
    }
}
