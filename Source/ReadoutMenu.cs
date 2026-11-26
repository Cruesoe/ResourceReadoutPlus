using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>The right-click menu on a readout row, in UI Rethemed's menu style.</summary>
[StaticConstructorOnStartup]
public static class ReadoutMenu
{
    private static readonly Texture2D MenuTex = CoreWidgets.Icon("dots-three");
    private static readonly Texture2D PinTex = CoreWidgets.Icon("push-pin");
    private static readonly Texture2D MinimumTex = CoreWidgets.Icon("warning");
    private static readonly Texture2D HistoryTex = CoreWidgets.Icon("chart-line-up");
    private static readonly Texture2D CombineTex = CoreWidgets.Icon("stack");
    private static readonly Texture2D GroupTex = CoreWidgets.Icon("folder");
    private static readonly Texture2D ReturnTex = CoreWidgets.Icon("arrow-counter-clockwise");
    private static readonly Texture2D HideTex = CoreWidgets.Icon("eye-slash");
    private static readonly Texture2D ShowTex = CoreWidgets.Icon("eye");
    private static readonly Texture2D RenameTex = CoreWidgets.Icon("pencil");
    private static readonly Texture2D DeleteTex = CoreWidgets.Icon("trash");
    private static readonly Texture2D NewTex = CoreWidgets.Icon("plus");
    private static readonly Texture2D ResetTex = CoreWidgets.Icon("arrow-clockwise");
    private static readonly Texture2D SettingsTex = CoreWidgets.Icon("gear-six");
    private static readonly Texture2D OrganizeTex = CoreWidgets.Icon("list-checks");
    private static readonly Texture2D TopTex = CoreWidgets.Icon("arrow-line-up");
    private static readonly Texture2D PromoteTex = CoreWidgets.Icon("arrow-left");
    private static readonly Texture2D DemoteTex = CoreWidgets.Icon("arrow-right");
    private static readonly Texture2D FilterTex = CoreWidgets.Icon("funnel");
    private static readonly Texture2D SearchTex = CoreWidgets.Icon("magnifying-glass");
    private static readonly Texture2D SeenTex = CoreWidgets.Icon("check");

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public static void Open(Row row)
    {
        Find.WindowStack.Add(new ChoiceMenu(row.label, row.icon ?? MenuTex, () => Sections(row)));
    }

    private static List<ChoiceSection> Sections(Row row)
    {
        List<ChoiceSection> sections = new List<ChoiceSection>();
        ChoiceSection own = new ChoiceSection { title = row.label };
        switch (row.kind)
        {
            case RowKind.Item:
            case RowKind.Family:
                ThingRows(row, own);
                break;
            case RowKind.Group:
                GroupRows(row, own);
                break;
            case RowKind.Category:
                CategoryRows(row, own);
                break;
        }

        if (own.items.Count > 0)
        {
            sections.Add(own);
        }

        if (!Settings.showTitle)
        {
            sections.Add(TitleRows());
            sections.Add(ModeRows());
        }

        sections.Add(ReadoutRows());
        return sections;
    }

    private static void ThingRows(Row row, ChoiceSection section)
    {
        string unit = row.key;
        bool pinned = Settings.pinned.Contains(unit);
        section.items.Add(Item(pinned ? "RRP_Unpin" : "RRP_Pin", PinTex, () =>
        {
            if (!Settings.pinned.Remove(unit))
            {
                Settings.pinned.Add(unit);
            }

            Settings.Changed();
        }));

        section.items.Add(Item("RRP_SetMinimum", MinimumTex, () => Find.WindowStack.Add(new Dialog_MinimumStock(row.key, row.label))));

        if (row.kind == RowKind.Item && row.def != null && row.mark != ResourceDiscoveries.None)
        {
            ThingDef def = row.def;
            section.items.Add(Item("RRP_MarkSeen", SeenTex, () => MarkSeen(def)));
        }

        if (row.kind == RowKind.Item && row.def != null)
        {
            ChoiceItem history = Item("RRP_History", HistoryTex, () => Find.WindowStack.Add(new Window_ResourceHistory(row.def)));
            history.enabled = Settings.trends;
            history.tip = Settings.trends ? null : "RRP_HistoryOffTip".Translate();
            section.items.Add(history);
        }

        CountAllItem(section, row.kind == RowKind.Family && row.category != null ? Keys.Of(row.category) : row.key, row.def);

        ThingCategoryDef? family = row.kind == RowKind.Family ? row.category : row.def?.FirstThingCategory;
        if (family != null && (row.kind == RowKind.Family || Settings.mode != ReadoutMode.Categorized))
        {
            bool combined = Settings.families.Contains(family.defName);
            ChoiceItem combine = Item(combined ? "RRP_Separate" : "RRP_Combine", CombineTex, () =>
            {
                if (!Settings.families.Remove(family.defName))
                {
                    Settings.families.Add(family.defName);
                }

                Settings.Changed();
            });
            combine.label = (combined ? "RRP_Separate" : "RRP_Combine").Translate(family.label);
            section.items.Add(combine);
        }

        if (Settings.mode == ReadoutMode.Grouped && row.list != Keys.PinnedList)
        {
            section.items.Add(Item("RRP_MoveToGroup", GroupTex, () => OpenGroupPicker(row)));
        }

        if (IsMoved(row))
        {
            section.items.Add(Item("RRP_ReturnToPlace", ReturnTex, () => ReturnToPlace(row)));
        }

        section.items.Add(Item("RRP_Hide", HideTex, () => Hide(row.key)));
    }

    private static void GroupRows(Row row, ChoiceSection section)
    {
        string group = Keys.GroupOf(row.key) ?? "";
        GroupInfo? info = ReadoutGroups.Find(group);
        section.items.Add(Item("RRP_Rename", RenameTex, () => Find.WindowStack.Add(new Dialog_GroupName("RRP_RenameGroup".Translate(), info?.label ?? row.label, label => ReadoutGroups.Rename(group, label)))));
        section.items.Add(Item("RRP_NewSubgroup", NewTex, () => Find.WindowStack.Add(new Dialog_GroupName("RRP_NewSubgroupTitle".Translate(info?.label ?? row.label), "", label => ReadoutGroups.NewGroup(label, group)))));
        if (info?.parent?.parent != null)
        {
            section.items.Add(Item("RRP_PromoteGroup", PromoteTex, () => ReadoutPanel.Promote(info)));
        }

        if (info?.parent != null)
        {
            section.items.Add(Item("RRP_MoveToTop", TopTex, () => ReadoutPanel.PlaceGroup(group, null, ReadoutGroups.TopLevel.Count > 0 ? ReadoutGroups.TopLevel[ReadoutGroups.TopLevel.Count - 1].rowKey : null)));
        }

        if (info != null && ReadoutPanel.GroupAbove(info) != null)
        {
            section.items.Add(Item("RRP_DemoteGroup", DemoteTex, () => ReadoutPanel.Demote(info)));
        }

        section.items.Add(Item("RRP_SetMinimum", MinimumTex, () => Find.WindowStack.Add(new Dialog_MinimumStock(row.key, row.label))));
        CountAllItem(section, row.key, null);
        section.items.Add(Item("RRP_HideGroup", HideTex, () => Hide(row.key)));
        if (info != null && info.custom)
        {
            section.items.Add(Item("RRP_DeleteGroup", DeleteTex, () => Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RRP_DeleteGroupConfirm".Translate(info.label), () => ReadoutGroups.Delete(group), destructive: true))));
        }
    }

    private static void CategoryRows(Row row, ChoiceSection section)
    {
        section.items.Add(Item("RRP_SetMinimum", MinimumTex, () => Find.WindowStack.Add(new Dialog_MinimumStock(row.key, row.label))));
        CountAllItem(section, row.key, null);
        if (IsMoved(row))
        {
            section.items.Add(Item("RRP_ReturnToPlace", ReturnTex, () => ReturnToPlace(row)));
        }

        section.items.Add(Item("RRP_Hide", HideTex, () => Hide(row.key)));
    }

    // A tick for counting all on the map; greyed out when a category or group above it already does.
    private static void CountAllItem(ChoiceSection section, string key, ThingDef? def)
    {
        bool own = Settings.countAll.Contains(key);
        bool inherited = !own && def != null && ReadoutCounts.IsCountedAll(def);
        ChoiceItem item = new ChoiceItem
        {
            label = "RRP_CountAll".Translate(),
            tick = true,
            on = own || inherited,
            enabled = !inherited,
            tip = inherited ? "RRP_CountAllInheritedTip".Translate() : "RRP_CountAllTip".Translate(),
            action = () =>
            {
                if (!Settings.countAll.Remove(key))
                {
                    Settings.countAll.Add(key);
                }

                Settings.Changed();
            },
        };
        section.items.Add(item);
    }

    private static ChoiceSection ReadoutRows()
    {
        ChoiceSection section = new ChoiceSection { title = "RRP_MenuReadout".Translate() };
        section.items.Add(Tick("RRP_ShowTitle", Settings.showTitle, () => Settings.showTitle = !Settings.showTitle));
        section.items.Add(Tick("RRP_AutoFold", Settings.autoFold, () => Settings.autoFold = !Settings.autoFold));
        section.items.Add(Tick("RRP_ShowNames", Settings.showNames, () => Settings.showNames = !Settings.showNames));
        section.items.Add(Tick("RRP_ShowZero", Settings.showZero, () => Settings.showZero = !Settings.showZero));
        section.items.Add(Tick("RRP_ShortNumbers", Settings.shortNumbers, () => Settings.shortNumbers = !Settings.shortNumbers));

        if (Settings.mode == ReadoutMode.Grouped)
        {
            section.items.Add(Item("RRP_NewGroup", NewTex, () => Find.WindowStack.Add(new Dialog_GroupName("RRP_NewGroupTitle".Translate(), "", label => ReadoutGroups.NewGroup(label)))));
        }

        if (AnyMarks())
        {
            section.items.Add(Item("RRP_MarkAllSeen", SeenTex, MarkAllSeen));
        }

        if (Settings.hidden.Count > 0)
        {
            ChoiceItem unhide = Item("RRP_Unhide", ShowTex, OpenUnhide);
            unhide.label = "RRP_Unhide".Translate(Settings.hidden.Count);
            section.items.Add(unhide);
        }

        if (HasOrder(Settings.mode))
        {
            ChoiceItem reset = Item("RRP_ResetOrder", ResetTex, () => ResetOrder(Settings.mode));
            reset.label = "RRP_ResetOrder".Translate(ReadoutPanel.ModeLabel(Settings.mode));
            section.items.Add(reset);
        }

        section.items.Add(Item("RRP_Organize", OrganizeTex, () => Find.WindowStack.Add(new Window_OrganizeResources())));
        section.items.Add(Item("RRP_OpenSettings", SettingsTex, () => Find.WindowStack.Add(new Dialog_ModSettings(ReadoutMod.Instance))));
        return section;
    }

    // Whether a list has anything for its reset to undo: a saved order, moved rows or moved groups.
    public static bool HasOrder(ReadoutMode mode)
    {
        return Settings.OrderFor(mode).Count > 0 || (mode == ReadoutMode.Categorized && Settings.parentOf.Count > 0)
            || (mode == ReadoutMode.Grouped && (Settings.groupOrder.Count > 0 || Settings.groupOf.Count > 0 || Settings.groupParents.Count > 0));
    }

    public static void ResetOrder(ReadoutMode mode)
    {
        Settings.OrderFor(mode).Clear();
        if (mode == ReadoutMode.Categorized)
        {
            Settings.parentOf.Clear();
        }
        else if (mode == ReadoutMode.Grouped)
        {
            Settings.groupOrder.Clear();
            Settings.groupOf.Clear();
            Settings.groupParents.Clear();
        }

        Settings.Changed();
    }

    private static void OpenGroupPicker(Row row)
    {
        string current = row.def != null ? ReadoutGroups.GroupOf(row.def) : "";
        OpenGroupPicker("RRP_MoveToGroup".Translate(), current, null, key => MoveToGroup(row, key), label => MoveToGroup(row, ReadoutGroups.NewGroup(label)));
    }

    // A menu of every group, indented by nesting; groups inside "except" are left out.
    public static void OpenGroupPicker(string title, string current, string? except, System.Action<string> pick, System.Action<string>? makeNew)
    {
        Find.WindowStack.Add(new ChoiceMenu(title, GroupTex, () =>
        {
            ChoiceSection section = new ChoiceSection();
            foreach (GroupInfo group in ReadoutGroups.All)
            {
                if (except != null && ReadoutGroups.IsWithin(group.key, except))
                {
                    continue;
                }

                string key = group.key;
                section.items.Add(new ChoiceItem
                {
                    label = new string(' ', group.depth * 4) + group.label,
                    icon = group.icon,
                    on = key == current,
                    action = () => pick(key),
                });
            }

            if (makeNew != null)
            {
                section.items.Add(Item("RRP_NewGroup", NewTex, () => Find.WindowStack.Add(new Dialog_GroupName("RRP_NewGroupTitle".Translate(), "", makeNew))));
            }

            return new List<ChoiceSection> { section };
        }));
    }

    public static void MoveToGroup(Row row, string group)
    {
        if (row.kind == RowKind.Family && row.members != null)
        {
            foreach (ThingDef member in row.members)
            {
                ReadoutPanel.SetGroup(member, group);
            }
        }
        else if (row.def != null)
        {
            ReadoutPanel.SetGroup(row.def, group);
        }

        Settings.Changed();
    }

    private static bool IsMoved(Row row)
    {
        if (Settings.mode == ReadoutMode.Categorized)
        {
            return Settings.parentOf.ContainsKey(row.key);
        }

        if (Settings.mode != ReadoutMode.Grouped)
        {
            return false;
        }

        if (row.kind == RowKind.Family && row.members != null)
        {
            foreach (ThingDef member in row.members)
            {
                if (Settings.groupOf.ContainsKey(Keys.Of(member)))
                {
                    return true;
                }
            }

            return false;
        }

        return Settings.groupOf.ContainsKey(row.key);
    }

    private static void ReturnToPlace(Row row)
    {
        if (Settings.mode == ReadoutMode.Categorized)
        {
            Settings.parentOf.Remove(row.key);
        }
        else if (row.kind == RowKind.Family && row.members != null)
        {
            foreach (ThingDef member in row.members)
            {
                Settings.groupOf.Remove(Keys.Of(member));
            }
        }
        else
        {
            Settings.groupOf.Remove(row.key);
        }

        Settings.Changed();
    }

    public static void Hide(string key)
    {
        if (!Settings.hidden.Contains(key))
        {
            Settings.hidden.Add(key);
        }

        Settings.pinned.Remove(key);
        Settings.Changed();
    }

    private static void OpenUnhide()
    {
        Find.WindowStack.Add(new ChoiceMenu("RRP_UnhideTitle".Translate(), ShowTex, () =>
        {
            ChoiceSection section = new ChoiceSection();
            foreach (string key in Settings.hidden)
            {
                string captured = key;
                section.items.Add(new ChoiceItem
                {
                    label = LabelOf(key),
                    action = () =>
                    {
                        Settings.hidden.Remove(captured);
                        Settings.Changed();
                    },
                });
            }

            section.items.SortBy(item => item.label);
            section.items.Add(Item("RRP_ShowAllHidden", ShowTex, () =>
            {
                Settings.hidden.Clear();
                Settings.Changed();
            }));
            return new List<ChoiceSection> { section };
        }));
    }

    public static string LabelOf(string key)
    {
        if (Keys.ThingOf(key) is ThingDef thing)
        {
            return thing.LabelCap;
        }

        if (Keys.CategoryOf(key) is ThingCategoryDef category)
        {
            return category.LabelCap;
        }

        if (Keys.GroupOf(key) is string group && ReadoutGroups.Find(group) is GroupInfo info)
        {
            return info.label;
        }

        return key;
    }

    private static ChoiceItem Item(string key, Texture icon, System.Action action)
    {
        return new ChoiceItem { label = key.Translate(), icon = icon, action = action };
    }

    // ---- Panel menu, from the title bar cog ------------------------------------------------

    public static void OpenPanel()
    {
        Find.WindowStack.Add(new ChoiceMenu("RRP_PanelMenu".Translate(), SettingsTex, () => Settings.showTitle
            ? new List<ChoiceSection> { ModeRows(), ReadoutRows() }
            : new List<ChoiceSection> { TitleRows(), ModeRows(), ReadoutRows() }));
    }

    // The title bar's search and filter buttons, for when the title bar is hidden.
    private static ChoiceSection TitleRows()
    {
        ChoiceSection section = new ChoiceSection { title = "RRP_MenuPanel".Translate() };
        section.items.Add(new ChoiceItem { label = "RRP_SearchHint".Translate(), icon = SearchTex, tick = true, on = ReadoutPanel.Searching, tip = "RRP_SearchTip".Translate(), action = ReadoutPanel.ToggleSearch });
        section.items.Add(Item("RRP_FilterMenu", FilterTex, OpenFilter));
        if (!Settings.autoFold)
        {
            section.items.Add(Item("RRP_FoldPanel", SidePanel.FoldLeftTex, () =>
            {
                Settings.folded = true;
                Settings.Changed();
            }));
        }

        return section;
    }

    private static ChoiceSection ModeRows()
    {
        ChoiceSection section = new ChoiceSection { title = "RRP_Mode".Translate() };
        foreach (ReadoutMode mode in System.Enum.GetValues(typeof(ReadoutMode)))
        {
            ReadoutMode captured = mode;
            section.items.Add(new ChoiceItem
            {
                label = ReadoutPanel.ModeTitle(mode),
                icon = ReadoutPanel.ModeIcon(mode),
                tip = ReadoutPanel.ModeTip(mode),
                on = Settings.mode == mode,
                action = () => ReadoutPanel.SetMode(captured),
            });
        }

        return section;
    }

    // ---- Filter menu, from the title bar --------------------------------------------------

    public static void OpenFilter()
    {
        Find.WindowStack.Add(new ChoiceMenu("RRP_FilterTitle".Translate(), FilterTex, FilterSections));
    }

    private static List<ChoiceSection> FilterSections()
    {
        ChoiceSection kinds = new ChoiceSection { title = "RRP_FilterShowOnly".Translate() };
        ChoiceItem all = Item("RRP_FilterAll", ShowTex, () =>
        {
            Settings.filtering = false;
            Settings.Changed();
        });
        all.on = !Settings.Filtering;
        all.tip = "RRP_FilterAllTip".Translate();
        kinds.items.Add(all);
        kinds.items.Add(FilterTick(ShowFilter.Trending, Settings.trends, "RRP_NeedsTrends"));
        kinds.items.Add(FilterTick(ShowFilter.Falling, Settings.trends, "RRP_NeedsTrends"));
        kinds.items.Add(FilterTick(ShowFilter.Low, true, null));
        kinds.items.Add(FilterTick(ShowFilter.New, Settings.newMarks, "RRP_NeedsNewMarks"));

        ChoiceSection options = new ChoiceSection { title = "RRP_FilterOptions".Translate() };
        options.items.Add(Tick("RRP_PinnedIgnoreFilter", Settings.pinnedIgnoreFilter, () => Settings.pinnedIgnoreFilter = !Settings.pinnedIgnoreFilter));
        ChoiceItem sort = Tick("RRP_SortByChange", Settings.Sorting, () => Settings.sortByChange = !Settings.sortByChange);
        if (!Settings.trends)
        {
            sort.enabled = false;
            sort.tip = "RRP_NeedsTrends".Translate();
        }

        options.items.Add(sort);
        if (AnyMarks())
        {
            options.items.Add(Item("RRP_MarkAllSeen", SeenTex, MarkAllSeen));
        }

        return new List<ChoiceSection> { kinds, options };
    }

    // One filter kind; ticking it also switches the filter on.
    private static ChoiceItem FilterTick(ShowFilter kind, bool available, string? needs)
    {
        string key = "RRP_Filter_" + kind;
        return new ChoiceItem
        {
            label = key.Translate(),
            tick = true,
            on = available && (Settings.filters & kind) != 0,
            enabled = available,
            tip = available || needs == null ? (key + "Tip").Translate() : needs.Translate(),
            action = () =>
            {
                Settings.filters ^= kind;
                Settings.filtering = Settings.filters != ShowFilter.None;
                Settings.Changed();
            },
        };
    }

    private static bool AnyMarks()
    {
        return Find.CurrentMap is Map map && ResourceDiscoveries.Current is ResourceDiscoveries discoveries && discoveries.AnyMarks(map);
    }

    private static void MarkSeen(ThingDef def)
    {
        if (Find.CurrentMap is Map map)
        {
            ResourceDiscoveries.Current?.Clear(map, def);
        }
    }

    private static void MarkAllSeen()
    {
        if (Find.CurrentMap is Map map)
        {
            ResourceDiscoveries.Current?.ClearAll(map);
        }
    }

    private static ChoiceItem Tick(string key, bool on, System.Action flip)
    {
        return new ChoiceItem
        {
            label = key.Translate(),
            tick = true,
            on = on,
            tip = (key + "Tip").Translate(),
            action = () =>
            {
                flip();
                Settings.Changed();
            },
        };
    }
}
