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
    public const float MinOpacity = 0.1f;
    public const int MinChartDays = 1;
    public const int MaxChartDays = 10;
    public const int UsualChartDays = 5;
    public const int MinNewHours = 1;
    public const int MaxNewHours = 240;
    public const int UsualNewHours = 24;
    public const int MaxNearPercent = 100;
    public const int UsualNearPercent = 25;

    // Matches the background of UI Rethemed - Notifications' alert and message rows.
    public const float DefaultOpacity = 0.6f;

    // Raised when new default look-alike families are added, so existing players get them once.
    public const int CurrentFamilies = 2;

    // Display
    public ReadoutMode mode = ReadoutMode.Grouped;
    public bool showNames = true;
    public float width = UsualWidth;
    public bool shortNumbers;
    public bool showZero;
    public bool folded;
    public float opacity = DefaultOpacity;

    // Counting
    public bool countHiddenInTotals = true;
    public bool countCarried;

    // Trends and minimum stock
    public bool trends = true;
    public int trendHours = UsualTrendHours;
    public bool lowStockAlert = true;
    public bool hoverChart = true;
    public int chartDays = UsualChartDays;
    public bool runOutTip = true;
    public bool runOutRow;

    // Filter: the kinds chosen, whether it is on, and how it treats pinned rows and rows close to their minimum.
    public ShowFilter filters;
    public bool filtering;
    public bool pinnedIgnoreFilter = true;
    public int nearPercent = UsualNearPercent;
    public bool sortByChange;

    // New and back-in-stock marks.
    public bool newMarks = true;
    public bool backInStock = true;
    public int newHours = UsualNewHours;

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

    // Built-in groups moved under another group (or to the top level, as ""), by group key.
    public Dictionary<string, string> groupParents = new Dictionary<string, string>();
    public int nextGroupId = 1;

    // Per row: hidden, counted map-wide, pinned to the top, minimum stock.
    public List<string> hidden = new List<string>();
    public List<string> countAll = new List<string>();
    public List<string> pinned = new List<string>();
    public Dictionary<string, int> minimums = new Dictionary<string, int>();

    // Categories whose things show as one combined row.
    public List<string> families = new List<string>();
    public bool familiesSet;
    public int familiesVersion;

    // Open state: groups start open, categories and families closed.
    public List<string> foldedGroups = new List<string>();
    public List<string> openCategories = new List<string>();
    public List<string> openFamilies = new List<string>();

    // Bumped by every change; caches rebuild on the next read.
    public int version;

    // Changes are written to disk at most this often, in real seconds; later ones wait for FlushIfDue or Flush.
    private const float WriteDelay = 2f;
    private bool unsaved;
    private float lastWrite = -WriteDelay;

    public void Changed()
    {
        version++;
        unsaved = true;
        FlushIfDue();
    }

    // Core calls this every frame before windows draw.
    public void FlushIfDue()
    {
        if (unsaved && UnityEngine.Time.realtimeSinceStartup - lastWrite >= WriteDelay)
        {
            Flush();
        }
    }

    public void Flush()
    {
        if (!unsaved)
        {
            return;
        }

        unsaved = false;
        lastWrite = UnityEngine.Time.realtimeSinceStartup;
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
        Scribe_Values.Look(ref opacity, "opacity", DefaultOpacity);
        Scribe_Values.Look(ref countHiddenInTotals, "countHiddenInTotals", true);
        Scribe_Values.Look(ref countCarried, "countCarried", false);
        Scribe_Values.Look(ref trends, "trends", true);
        Scribe_Values.Look(ref trendHours, "trendHours", UsualTrendHours);
        Scribe_Values.Look(ref lowStockAlert, "lowStockAlert", true);
        Scribe_Values.Look(ref hoverChart, "hoverChart", true);
        Scribe_Values.Look(ref chartDays, "chartDays", UsualChartDays);
        Scribe_Values.Look(ref runOutTip, "runOutTip", true);
        Scribe_Values.Look(ref runOutRow, "runOutRow", false);
        Scribe_Values.Look(ref filters, "filters", ShowFilter.None);
        Scribe_Values.Look(ref filtering, "filtering", false);
        Scribe_Values.Look(ref pinnedIgnoreFilter, "pinnedIgnoreFilter", true);
        Scribe_Values.Look(ref nearPercent, "nearPercent", UsualNearPercent);
        Scribe_Values.Look(ref sortByChange, "sortByChange", false);
        Scribe_Values.Look(ref newMarks, "newMarks", true);
        Scribe_Values.Look(ref backInStock, "backInStock", true);
        Scribe_Values.Look(ref newHours, "newHours", UsualNewHours);

        Scribe_Collections.Look(ref orderGrouped, "orderGrouped", LookMode.Value);
        Scribe_Collections.Look(ref orderSimple, "orderSimple", LookMode.Value);
        Scribe_Collections.Look(ref orderCategorized, "orderCategorized", LookMode.Value);
        Scribe_Collections.Look(ref groupOf, "groupOf", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref parentOf, "parentOf", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref groupOrder, "groupOrder", LookMode.Value);
        Scribe_Collections.Look(ref customGroups, "customGroups", LookMode.Deep);
        Scribe_Collections.Look(ref groupLabels, "groupLabels", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref groupParents, "groupParents", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref nextGroupId, "nextGroupId", 1);
        Scribe_Collections.Look(ref hidden, "hidden", LookMode.Value);
        Scribe_Collections.Look(ref countAll, "countAll", LookMode.Value);
        Scribe_Collections.Look(ref pinned, "pinned", LookMode.Value);
        Scribe_Collections.Look(ref minimums, "minimums", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref families, "families", LookMode.Value);
        Scribe_Values.Look(ref familiesSet, "familiesSet", false);
        Scribe_Values.Look(ref familiesVersion, "familiesVersion", 0);
        Scribe_Collections.Look(ref foldedGroups, "foldedGroups", LookMode.Value);
        Scribe_Collections.Look(ref openCategories, "openCategories", LookMode.Value);
        Scribe_Collections.Look(ref openFamilies, "openFamilies", LookMode.Value);

        orderGrouped ??= new List<string>();
        orderSimple ??= new List<string>();
        orderCategorized ??= new List<string>();
        groupOf ??= new Dictionary<string, string>();
        parentOf ??= new Dictionary<string, string>();
        groupOrder ??= new List<string>();
        customGroups ??= new List<CustomGroup>();
        groupLabels ??= new Dictionary<string, string>();
        groupParents ??= new Dictionary<string, string>();
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

    // The filter is applied: switched on with at least one kind chosen.
    public bool Filtering => filtering && filters != ShowFilter.None;

    // Rows are sorted by how much they changed (needs trends).
    public bool Sorting => sortByChange && trends;

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
