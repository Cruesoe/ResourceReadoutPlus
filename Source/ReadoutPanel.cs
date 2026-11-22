using System;
using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Draws the readout in place of the base game's, as a foldable UI Rethemed side panel at the top left.</summary>
[StaticConstructorOnStartup]
public static partial class ReadoutPanel
{
    public const string PackageId = "cruesoe.resourcereadoutplus";
    private const string Part = "Resource Readout+";
    private const string SearchControl = "RRP_Search";

    private const float Left = 2f;
    private const float Top = 7f;
    private const float BottomRoom = 200f;
    private const float NarrowWidth = 150f;

    // Title bar room: three 32 px tools, the fold button and the title icon with its margin.
    private const float ToolsWidth = 96f;
    private const float FoldRoom = 32f;
    private const float IconRoom = 32f;
    private const float FoldedHeight = 70f;
    private const float TitleHeight = 30f;
    private const float SearchHeight = 28f;
    private const float Gap = 4f;
    public const float RowHeight = 24f;

    private static readonly Texture2D TitleTex = CoreWidgets.Icon("stack");
    private static readonly Texture2D SearchTex = CoreWidgets.Icon("magnifying-glass");
    private static readonly Texture2D FilterTex = CoreWidgets.Icon("funnel");
    private static readonly Texture2D SettingsTex = CoreWidgets.Icon("gear-six");
    private static readonly Texture2D GroupedTex = CoreWidgets.Icon("squares-four");
    private static readonly Texture2D SimpleTex = CoreWidgets.Icon("list");
    private static readonly Texture2D CategorizedTex = CoreWidgets.Icon("tree-structure");

    private static Vector2 scroll;
    private static bool searching;
    private static Rect panelRect;
    private static Rect outRect;
    private static float contentHeight;
    private static bool interactive;

    // Where the panel ended on the last frame it was drawn, for placing the top-left messages beside it.
    private const float MessageGap = 8f;
    private static float drawnRight;
    private static int drawnFrame = -10;
    private static readonly Action Draw = DrawPanel;

    // Translated once per language.
    private static string? title;
    private static string? searchHint;
    private static string? searchTip;
    private static string? empty;
    private static string? settingsTitle;
    private static string? settingsTip;
    private static string? filterTitle;
    private static string? filterEmpty;
    private static string? filterTip;
    private static KeyCode filterTipFor;
    private static LoadedLanguage? textLanguage;

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public static void Forget()
    {
        scroll = Vector2.zero;
        searching = false;
        ResetPress();
    }

    // True when this drew the readout (or nothing was due); false hands over to the base game's readout.
    public static bool OnGUI()
    {
        if (Event.current.type == EventType.Layout || Current.ProgramState != ProgramState.Playing || Find.MainTabsRoot.OpenTab == MainButtonDefOf.Menu)
        {
            return true;
        }

        if (Find.CurrentMap == null)
        {
            return true;
        }

        if (Failsafe.Failed(Part))
        {
            return false;
        }

        Text.Font = GameFont.Small;
        Theme.BeginMod(ReadoutMod.Instance);
        try
        {
            Failsafe.Run(Part, Draw);
        }
        finally
        {
            Theme.CloseScrolls();
            Theme.Reset();
            Theme.EndMod();
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        return true;
    }

    private static void Translate()
    {
        if (textLanguage == LanguageDatabase.activeLanguage && title != null)
        {
            return;
        }

        textLanguage = LanguageDatabase.activeLanguage;
        title = "RRP_Title".Translate();
        searchHint = "RRP_SearchHint".Translate();
        searchTip = "RRP_SearchTip".Translate();
        empty = "RRP_Empty".Translate();
        settingsTitle = "RRP_PanelMenu".Translate();
        settingsTip = "RRP_PanelMenuTip".Translate();
        filterTitle = "RRP_FilterTitle".Translate();
        filterEmpty = "RRP_FilterEmpty".Translate();
        filterTip = null;
    }

    private static readonly string[] ModeKeys = { "RRP_Mode_Grouped", "RRP_Mode_Simple", "RRP_Mode_Categorized" };

    // A list's name in lower case, for use inside sentences.
    public static string ModeLabel(ReadoutMode mode)
    {
        return Translations.Of(ModeKeys[(int)mode]);
    }

    public static string ModeTitle(ReadoutMode mode)
    {
        return Translations.Capital(ModeKeys[(int)mode]);
    }

    public static string ModeTip(ReadoutMode mode)
    {
        return Translations.Tip(ModeKeys[(int)mode]);
    }

    public static void SetMode(ReadoutMode mode)
    {
        Settings.mode = mode;
        scroll = Vector2.zero;
        Settings.Changed();
    }

    public static Texture2D ModeIcon(ReadoutMode mode)
    {
        return mode switch
        {
            ReadoutMode.Simple => SimpleTex,
            ReadoutMode.Categorized => CategorizedTex,
            _ => GroupedTex,
        };
    }

    private static void DrawPanel()
    {
        Translate();
        FilterKey();
        Map map = Find.CurrentMap;
        ReadoutData.Ensure(map);
        interactive = !Find.WindowStack.MouseObscuredNow;

        List<Row> rows = ReadoutData.Rows;
        contentHeight = Mathf.Max(RowHeight, rows.Count * RowHeight);
        float width = Settings.folded ? SidePanel.FoldedWidth : Settings.showNames ? Settings.width : NarrowWidth;
        float most = UI.screenHeight - Top - BottomRoom;
        float wanted = Settings.folded ? FoldedHeight : TitleHeight + Gap * 2f + (searching ? SearchHeight + Gap : 0f) + contentHeight;
        panelRect = new Rect(Left, Top, width, Mathf.Min(wanted, most));
        drawnRight = panelRect.xMax;
        drawnFrame = Time.frameCount;

        bool folded = Settings.folded;
        float surface = CoreWidgets.SurfaceAlpha;
        CoreWidgets.SurfaceAlpha = Opacity;
        try
        {
            // The title icon only when the tools leave room for it; the folded strip always has it. The open panel has no title text, only the folded strip's tooltip names it.
            Texture2D? icon = folded || width - FoldRoom - ToolsWidth >= IconRoom ? TitleTex : null;
            bool open = SidePanel.Begin(panelRect, folded ? title : null, icon, ref folded, SidePanel.Side.Left, out Rect inner, out Rect titleBar, out float right, tools: ToolsWidth);
            if (folded != Settings.folded)
            {
                Settings.folded = folded;
                Settings.Changed();
            }

            if (open)
            {
                TitleTools(titleBar, right);
                DrawBody(inner, rows);
            }
        }
        finally
        {
            CoreWidgets.SurfaceAlpha = surface;
        }

        if (interactive)
        {
            CoreWidgets.TakeClicks(panelRect);
        }
    }

    // The messages' left edge: right of the panel when it was drawn this frame or the last, else the base game's.
    public static float MessagesX(float usual)
    {
        return Time.frameCount - drawnFrame <= 1 ? Mathf.Max(usual, drawnRight + MessageGap) : usual;
    }

    // The panel's background opacity, from the settings slider.
    public static float Opacity => Mathf.Clamp(Settings.opacity, ReadoutSettings.MinOpacity, 1f);

    private static void TitleTools(Rect titleBar, float right)
    {
        if (CoreWidgets.TitleTool(titleBar, ref right, SettingsTex, settingsTitle, settingsTip))
        {
            ReadoutMenu.OpenPanel();
        }

        if (CoreWidgets.TitleTool(titleBar, ref right, FilterTex, filterTitle, FilterTip(), on: Settings.Filtering))
        {
            ReadoutMenu.OpenFilter();
        }

        if (CoreWidgets.TitleTool(titleBar, ref right, SearchTex, searchHint, searchTip, on: searching))
        {
            searching = !searching;
            if (!searching)
            {
                ReadoutData.search = "";
            }
            else
            {
                Failsafe.Later(() => GUI.FocusControl(SearchControl));
            }
        }
    }

    // The filter button's tooltip, naming the hotkey; rebuilt when the binding changes.
    private static string FilterTip()
    {
        KeyCode key = ReadoutDefOf.RRP_ToggleFilter.MainKey;
        if (filterTip == null || filterTipFor != key)
        {
            filterTipFor = key;
            filterTip = key == KeyCode.None ? "RRP_FilterTip".Translate() : "RRP_FilterTipKey".Translate(ReadoutDefOf.RRP_ToggleFilter.MainKeyLabel);
        }

        return filterTip;
    }

    // The hotkey switches between the full list and the chosen filter; with no filter chosen it opens the filter menu.
    private static void FilterKey()
    {
        if (Event.current.type != EventType.KeyDown || GUIUtility.keyboardControl != 0 || !ReadoutDefOf.RRP_ToggleFilter.KeyDownEvent)
        {
            return;
        }

        Event.current.Use();
        if (Settings.filters == ShowFilter.None)
        {
            ReadoutMenu.OpenFilter();
            return;
        }

        Settings.filtering = !Settings.filtering;
        scroll = Vector2.zero;
        Settings.Changed();
    }

    private static void DrawBody(Rect inner, List<Row> rows)
    {
        Rect body = inner;
        if (searching)
        {
            Rect line = new Rect(inner.x, inner.y, inner.width, SearchHeight);
            SearchField.Draw(line, ref ReadoutData.search, SearchControl, searchHint);
            body.yMin = line.yMax + Gap;
        }

        outRect = body;
        if (rows.Count == 0)
        {
            CoreWidgets.Empty(body, Settings.Filtering ? filterEmpty : empty);
            return;
        }

        if (dragging)
        {
            ListDrag.ScrollNearEdges(outRect, ref scroll, contentHeight);
        }

        Rect view = Theme.BeginScroll(outRect, ref scroll, contentHeight);
        try
        {
            float top = scroll.y - RowHeight;
            float bottom = scroll.y + outRect.height;
            for (int i = 0; i < rows.Count; i++)
            {
                float y = i * RowHeight;
                if (y < top || y > bottom)
                {
                    continue;
                }

                Rect rect = new Rect(0f, y, view.width, RowHeight);
                DrawRow(rows[i], rect, i);
                if (interactive)
                {
                    RowInput(rows[i], rect);
                }
            }

            DragInput(rows, view.width);
        }
        finally
        {
            Theme.EndScroll();
        }
    }
}
