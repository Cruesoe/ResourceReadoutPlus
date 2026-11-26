using System;
using System.Collections.Generic;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>The mod settings page, as UI Rethemed settings cards in two columns.</summary>
[StaticConstructorOnStartup]
public static class SettingsPage
{
    private static readonly Texture2D DisplayTex = CoreWidgets.Icon("eye");
    private static readonly Texture2D CountingTex = CoreWidgets.Icon("hash");
    private static readonly Texture2D TrendsTex = CoreWidgets.Icon("chart-line-up");
    private static readonly Texture2D StockTex = CoreWidgets.Icon("warning");
    private static readonly Texture2D ResetTex = CoreWidgets.Icon("arrow-clockwise");
    private static readonly Texture2D ModeTex = CoreWidgets.Icon("rows");
    private static readonly Texture2D OrganizeTex = CoreWidgets.Icon("list-checks");
    private static readonly Texture2D HiddenTex = CoreWidgets.Icon("eye-slash");

    // Session lifetime: which cards are open, starting all open.
    private static readonly List<string> Open = new List<string> { "Organize", "Display", "Filter", "Counting", "Trends", "New", "Stock", "Reset" };
    private static readonly Texture2D FilterTex = CoreWidgets.Icon("funnel");
    private static readonly Texture2D NewTex = CoreWidgets.Icon("sparkle");
    private static readonly ShowFilter[] FilterKinds = { ShowFilter.Trending, ShowFilter.Falling, ShowFilter.Low, ShowFilter.New };
    private static readonly string[] FilterKeys = { "RRP_Filter_Trending", "RRP_Filter_Falling", "RRP_Filter_Low", "RRP_Filter_New" };
    private static SettingsCards? cards;
    private static Vector2 leftScroll;
    private static Vector2 rightScroll;
    private static float leftHeight;
    private static float rightHeight;
    private static readonly Func<float, float, float> LeftCards = DrawLeft;
    private static readonly Func<float, float, float> RightCards = DrawRight;

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public static void Draw(Rect inRect)
    {
        cards ??= new SettingsCards("ResourceReadoutPlus", () => Open);
        Theme.BeginMod(ReadoutMod.Instance);
        try
        {
            float columnWidth = Mathf.Floor((inRect.width - 20f) / 2f);
            Rect left = new Rect(inRect.x, inRect.y, columnWidth, inRect.height);
            Rect right = new Rect(inRect.xMax - columnWidth, inRect.y, columnWidth, inRect.height);
            leftHeight = cards.Column(left, ref leftScroll, leftHeight, LeftCards);
            rightHeight = cards.Column(right, ref rightScroll, rightHeight, RightCards);
        }
        finally
        {
            Theme.CloseScrolls();
            Theme.Reset();
            Theme.EndMod();
        }

        cards.Finish();
    }

    private static float DrawLeft(float y, float width)
    {
        y = cards!.Card(y, width, "Organize", OrganizeTex, Translations.Of("RRP_SectionOrganize"), null, Organize);
        y = cards.Card(y, width, "Display", DisplayTex, Translations.Of("RRP_SectionDisplay"), null, Display);
        y = cards.Card(y, width, "Filter", FilterTex, Translations.Of("RRP_SectionFilter"), null, Filter);
        y = cards.Card(y, width, "Counting", CountingTex, Translations.Of("RRP_SectionCounting"), null, Counting);
        return y;
    }

    private static float DrawRight(float y, float width)
    {
        y = cards!.Card(y, width, "Trends", TrendsTex, Translations.Of("RRP_SectionTrends"), null, Trends);
        y = cards.Card(y, width, "New", NewTex, Translations.Of("RRP_SectionNew"), null, NewResources);
        y = cards.Card(y, width, "Stock", StockTex, Translations.Of("RRP_SectionStock"), null, Stock);
        y = cards.Card(y, width, "Reset", ResetTex, Translations.Of("RRP_SectionReset"), null, Reset);
        return y;
    }

    private static void Organize(Listing_Standard listing)
    {
        SettingsCards.Hint(listing, Translations.Of("RRP_OrganizeHint"));
        if (Button(listing, Translations.Of("RRP_Organize"), OrganizeTex))
        {
            cards!.Later(() => Find.WindowStack.Add(new Window_OrganizeResources()));
        }

        listing.Gap(4f);
        if (Button(listing, Translations.Of("RRP_ShowHiddenList", Settings.hidden.Count), HiddenTex))
        {
            cards!.Later(() => Find.WindowStack.Add(new Window_OrganizeResources(Window_OrganizeResources.ShowHidden)));
        }
    }

    private static bool Button(Listing_Standard listing, string label, Texture icon)
    {
        Rect line = listing.GetRect(30f);
        return CoreWidgets.Button(new Rect(line.x, line.y, Mathf.Min(line.width, CoreWidgets.ButtonWidth(label, true)), line.height), label, icon);
    }

    private static void Display(Listing_Standard listing)
    {
        Rect line = listing.GetRect(30f);
        cards!.Dropdown(line, Translations.Of("RRP_Mode"), ReadoutPanel.ModeTitle(Settings.mode), ModeTex, Translations.Of("RRP_ModeSettingTip"), ModeChoices);
        listing.Gap(4f);
        Tick(listing, "RRP_ShowTitle", ref Settings.showTitle);
        Tick(listing, "RRP_AutoFold", ref Settings.autoFold);
        Tick(listing, "RRP_ShowNames", ref Settings.showNames);
        cards.Slider(listing, Translations.Of("RRP_Width"), Translations.Of("RRP_WidthTip"), Settings.width, ReadoutSettings.MinWidth, ReadoutSettings.MaxWidth, 10f,
            value => Mathf.RoundToInt(value).ToStringCached(), ReadoutSettings.UsualWidth, value =>
            {
                Settings.width = value;
                Settings.Changed();
            });
        cards.Slider(listing, Translations.Of("RRP_Opacity"), Translations.Of("RRP_OpacityTip"), Settings.opacity, ReadoutSettings.MinOpacity, 1f, 0.05f,
            value => Translations.Of("RRP_Percent", Mathf.RoundToInt(value * 100f)), ReadoutSettings.DefaultOpacity, value =>
            {
                Settings.opacity = value;
                Settings.Changed();
            });
        Tick(listing, "RRP_ShortNumbers", ref Settings.shortNumbers);
        Tick(listing, "RRP_ShowZero", ref Settings.showZero);
    }

    private static List<ChoiceSection> ModeChoices()
    {
        ChoiceSection section = new ChoiceSection();
        foreach (ReadoutMode mode in Enum.GetValues(typeof(ReadoutMode)))
        {
            ReadoutMode captured = mode;
            section.items.Add(new ChoiceItem
            {
                label = ReadoutPanel.ModeTitle(mode),
                tip = ReadoutPanel.ModeTip(mode),
                on = Settings.mode == mode,
                action = () =>
                {
                    Settings.mode = captured;
                    Settings.Changed();
                },
            });
        }

        return new List<ChoiceSection> { section };
    }

    private static void Counting(Listing_Standard listing)
    {
        Tick(listing, "RRP_CountHiddenInTotals", ref Settings.countHiddenInTotals);
        Tick(listing, "RRP_CountCarried", ref Settings.countCarried);
        if (SettingsCards.ResetButton(listing, Translations.Of("RRP_ClearCountAll", Settings.countAll.Count), Settings.countAll.Count > 0, Translations.Of("RRP_ClearCountAllTip"), Translations.Of("RRP_NothingToClear")))
        {
            Settings.countAll.Clear();
            Settings.Changed();
        }
    }

    private static void Trends(Listing_Standard listing)
    {
        Tick(listing, "RRP_Trends", ref Settings.trends);
        cards!.Slider(listing, Translations.Of("RRP_TrendHours"), Translations.Of("RRP_TrendHoursTip"), Settings.trendHours, ReadoutSettings.MinTrendHours, ReadoutSettings.MaxTrendHours, 6f,
            value => Translations.Of("RRP_Hours", Mathf.RoundToInt(value)), ReadoutSettings.UsualTrendHours, value =>
            {
                Settings.trendHours = Mathf.RoundToInt(value);
                Settings.Changed();
            });

        bool trends = Settings.trends;
        TickIf(listing, "RRP_HoverChart", ref Settings.hoverChart, trends, "RRP_NeedsTrends");
        Greyed(trends && Settings.hoverChart, () => cards.Slider(listing, Translations.Of("RRP_ChartDays"), Translations.Of("RRP_ChartDaysTip"), Settings.chartDays,
            ReadoutSettings.MinChartDays, ReadoutSettings.MaxChartDays, 1f, value => Translations.Of("RRP_Days", Mathf.RoundToInt(value)), ReadoutSettings.UsualChartDays, value =>
            {
                Settings.chartDays = Mathf.RoundToInt(value);
                Settings.Changed();
            }));
        TickIf(listing, "RRP_RunOutInTooltip", ref Settings.runOutTip, trends, "RRP_NeedsTrends");
        TickIf(listing, "RRP_RunOutRow", ref Settings.runOutRow, trends, "RRP_NeedsTrends");
        TickIf(listing, "RRP_SortByChange", ref Settings.sortByChange, trends, "RRP_NeedsTrends");
    }

    private static void Filter(Listing_Standard listing)
    {
        SettingsCards.Hint(listing, Translations.Of("RRP_FilterHint"));
        for (int i = 0; i < FilterKinds.Length; i++)
        {
            ShowFilter kind = FilterKinds[i];
            string key = FilterKeys[i];
            bool available = kind == ShowFilter.Low || (kind == ShowFilter.New ? Settings.newMarks : Settings.trends);
            bool on = (Settings.filters & kind) != 0;
            bool before = on;
            if (available)
            {
                SettingsCards.Tick(listing, Translations.Of(key), ref on, Translations.Tip(key));
            }
            else
            {
                SettingsCards.Tick(listing, null, Theme.TextDim, Translations.Of(key), ref on, Translations.Of(kind == ShowFilter.New ? "RRP_NeedsNewMarks" : "RRP_NeedsTrends"), false);
            }

            if (on != before)
            {
                Settings.filters ^= kind;
                Settings.filtering = Settings.filters != ShowFilter.None;
                Settings.Changed();
            }
        }

        Tick(listing, "RRP_Filtering", ref Settings.filtering);
        Tick(listing, "RRP_PinnedIgnoreFilter", ref Settings.pinnedIgnoreFilter);
        cards!.Slider(listing, Translations.Of("RRP_NearPercent"), Translations.Of("RRP_NearPercentTip"), Settings.nearPercent, 0f, ReadoutSettings.MaxNearPercent, 5f,
            value => Translations.Of("RRP_Percent", Mathf.RoundToInt(value)), ReadoutSettings.UsualNearPercent, value =>
            {
                Settings.nearPercent = Mathf.RoundToInt(value);
                Settings.Changed();
            });
    }

    private static void NewResources(Listing_Standard listing)
    {
        Tick(listing, "RRP_NewMarks", ref Settings.newMarks);
        TickIf(listing, "RRP_BackInStock", ref Settings.backInStock, Settings.newMarks, "RRP_NeedsNewMarks");
        Greyed(Settings.newMarks, () => cards!.Slider(listing, Translations.Of("RRP_NewHours"), Translations.Of("RRP_NewHoursTip"), Settings.newHours,
            ReadoutSettings.MinNewHours, ReadoutSettings.MaxNewHours, 1f, value => Translations.Of("RRP_Hours", Mathf.RoundToInt(value)), ReadoutSettings.UsualNewHours, value =>
            {
                Settings.newHours = Mathf.RoundToInt(value);
                Settings.Changed();
            }));
    }

    // A tick that needs another setting: greyed out and inert with a tooltip naming it when that is off.
    private static void TickIf(Listing_Standard listing, string key, ref bool value, bool enabled, string needs)
    {
        if (enabled)
        {
            Tick(listing, key, ref value);
            return;
        }

        bool shown = value;
        SettingsCards.Tick(listing, null, Theme.TextDim, Translations.Of(key), ref shown, Translations.Of(needs), false);
    }

    // Draws a control greyed out and inert unless enabled.
    private static void Greyed(bool enabled, Action draw)
    {
        bool wasEnabled = GUI.enabled;
        Color wasColor = GUI.color;
        if (!enabled)
        {
            GUI.enabled = false;
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
        }

        try
        {
            draw();
        }
        finally
        {
            GUI.enabled = wasEnabled;
            GUI.color = wasColor;
        }
    }

    private static void Stock(Listing_Standard listing)
    {
        Tick(listing, "RRP_LowStockAlertSetting", ref Settings.lowStockAlert);
        if (SettingsCards.ResetButton(listing, Translations.Of("RRP_ClearMinimums", Settings.minimums.Count), Settings.minimums.Count > 0, Translations.Of("RRP_ClearMinimumsTip"), Translations.Of("RRP_NothingToClear")))
        {
            cards!.Confirm(Translations.Of("RRP_ClearMinimumsConfirm"), () =>
            {
                Settings.minimums.Clear();
                Settings.Changed();
            });
        }
    }

    private static void Reset(Listing_Standard listing)
    {
        foreach (ReadoutMode mode in Enum.GetValues(typeof(ReadoutMode)))
        {
            ReadoutMode captured = mode;
            if (SettingsCards.ResetButton(listing, Translations.Of("RRP_ResetOrder", ReadoutPanel.ModeLabel(mode)), ReadoutMenu.HasOrder(mode), Translations.Of("RRP_ResetOrderTip"), Translations.Of("RRP_NothingToReset")))
            {
                ReadoutMenu.ResetOrder(captured);
            }
        }

        bool groups = Settings.customGroups.Count > 0 || Settings.groupLabels.Count > 0 || Settings.groupParents.Count > 0;
        if (SettingsCards.ResetButton(listing, Translations.Of("RRP_ResetGroups"), groups, Translations.Of("RRP_ResetGroupsTip"), Translations.Of("RRP_NothingToReset")))
        {
            cards!.Confirm(Translations.Of("RRP_ResetGroupsConfirm"), () =>
            {
                foreach (CustomGroup group in Settings.customGroups.ToArray())
                {
                    ReadoutGroups.Delete(group.key);
                }

                Settings.groupLabels.Clear();
                Settings.groupParents.Clear();
                Settings.Changed();
            });
        }

        if (SettingsCards.ResetButton(listing, Translations.Of("RRP_ShowAllHiddenCount", Settings.hidden.Count), Settings.hidden.Count > 0, Translations.Of("RRP_ShowAllHiddenTip"), Translations.Of("RRP_NothingToReset")))
        {
            Settings.hidden.Clear();
            Settings.Changed();
        }

        if (SettingsCards.ResetButton(listing, Translations.Of("RRP_UnpinAll", Settings.pinned.Count), Settings.pinned.Count > 0, Translations.Of("RRP_UnpinAllTip"), Translations.Of("RRP_NothingToReset")))
        {
            Settings.pinned.Clear();
            Settings.Changed();
        }

        if (SettingsCards.ResetButton(listing, Translations.Of("RRP_ResetFamilies"), FamiliesChanged(), Translations.Of("RRP_ResetFamiliesTip"), Translations.Of("RRP_NothingToReset")))
        {
            Settings.families.Clear();
            Settings.families.AddRange(ReadoutMod.UsualFamilies);
            Settings.Changed();
        }
    }

    // The combined categories differ from the usual set.
    private static bool FamiliesChanged()
    {
        List<string> families = Settings.families;
        if (families.Count != ReadoutMod.UsualFamilies.Length)
        {
            return true;
        }

        foreach (string family in ReadoutMod.UsualFamilies)
        {
            if (!families.Contains(family))
            {
                return true;
            }
        }

        return false;
    }

    private static void Tick(Listing_Standard listing, string key, ref bool value)
    {
        bool before = value;
        SettingsCards.Tick(listing, Translations.Of(key), ref value, Translations.Tip(key));
        if (before != value)
        {
            Settings.Changed();
        }
    }
}
