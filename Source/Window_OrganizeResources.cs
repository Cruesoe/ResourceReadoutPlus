using System;
using System.Collections.Generic;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>
/// Organizes the grouped readout away from the map: the group tree on the left (make, rename, nest, reorder, delete),
/// and on the right the selected group's resources to drag into order or hide, every resource to search, or everything hidden to show again.
/// </summary>
[StaticConstructorOnStartup]
public class Window_OrganizeResources : Window
{
    private const string Part = "Resource Readout+ organizer";
    private const float Pad = 8f;
    private const float TitleHeight = 40f;
    private const float LeftWidth = 290f;
    private const float RowHeight = 30f;
    private const float ToolSize = 28f;
    private const float SearchHeight = 28f;
    private const string SearchControl = "RRP_OrganizeSearch";
    private const string AllView = "*all";
    private const string HiddenView = "*hidden";

    private static readonly Texture2D TitleTex = CoreWidgets.Icon("list-checks");
    private static readonly Texture2D AllTex = CoreWidgets.Icon("list");
    private static readonly Texture2D HiddenTex = CoreWidgets.Icon("eye-slash");
    private static readonly Texture2D ShowTex = CoreWidgets.Icon("eye");
    private static readonly Texture2D FolderTex = CoreWidgets.Icon("folder");
    private static readonly Texture2D NewTex = CoreWidgets.Icon("plus");
    private static readonly Texture2D SubTex = CoreWidgets.Icon("list-plus");
    private static readonly Texture2D RenameTex = CoreWidgets.Icon("pencil");
    private static readonly Texture2D MoveTex = CoreWidgets.Icon("arrows-out-cardinal");
    private static readonly Texture2D TopTex = CoreWidgets.Icon("arrow-line-up");
    private static readonly Texture2D UpTex = CoreWidgets.Icon("arrow-up");
    private static readonly Texture2D DownTex = CoreWidgets.Icon("arrow-down");
    private static readonly Texture2D DeleteTex = CoreWidgets.Icon("trash");

    // Session lifetime: every thing the readout can count.
    private static List<ThingDef>? countable;

    private readonly Action draw;
    private readonly Action close;
    private readonly OrderList<ReadoutUnit> order;
    private string selected = AllView;
    private string search = "";
    private Vector2 treeScroll;
    private Vector2 listScroll;
    private Rect inner;

    // The right-hand list, rebuilt when settings, the view or the search change.
    private readonly List<ReadoutUnit> units = new List<ReadoutUnit>();
    private readonly List<string> hiddenKeys = new List<string>();
    private int builtFor = -1;
    private int notesFor = -1;
    private string builtView = "";
    private string builtSearch = "";

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public const string ShowHidden = HiddenView;

    // Opens on the first group, or on the given view (ShowHidden for the hidden list).
    public Window_OrganizeResources(string? view = null)
    {
        doWindowBackground = false;
        forcePause = false;
        draggable = true;
        preventCameraMotion = false;
        closeOnClickedOutside = false;
        draw = DrawContents;
        close = () => Failsafe.Later(() => Close());
        order = new OrderList<ReadoutUnit>(unit => unit.label, IconOf)
        {
            Hidden = unit => ReadoutData.IsHidden(unit.key),
            ToggleHidden = unit => ToggleHidden(unit.key),
            Note = unit => unit.note,
            IconAsIs = unit => true,
        };
        if (view != null)
        {
            selected = view;
        }
        else if (ReadoutGroups.TopLevel.Count > 0)
        {
            selected = ReadoutGroups.TopLevel[0].key;
        }
    }

    protected override float Margin => 0f;

    public override Vector2 InitialSize => new Vector2(Mathf.Min(900f, UI.screenWidth - 40f), Mathf.Min(680f, UI.screenHeight - 40f));

    public override void DoWindowContents(Rect inRect)
    {
        inner = inRect;
        if (Failsafe.Failed(Part))
        {
            Rounded.WindowBackground(inRect);
            CoreWidgets.Empty(inRect, "RRP_OrganizeFailed".Translate());
            return;
        }

        Theme.BeginMod(ReadoutMod.Instance);
        try
        {
            Failsafe.Run(Part, draw);
        }
        finally
        {
            Theme.CloseScrolls();
            Theme.Reset();
            Theme.EndMod();
        }
    }

    private void DrawContents()
    {
        SearchField.StopOnClickAway();
        Rounded.WindowBackground(inner);
        Rect rect = inner.ContractedBy(Pad);
        CoreWidgets.TitleBar(new Rect(rect.x, rect.y, rect.width, TitleHeight), TitleTex, "RRP_OrganizeTitle".Translate(), float.MaxValue, close);
        Rect body = new Rect(rect.x, rect.y + TitleHeight + Pad, rect.width, rect.height - TitleHeight - Pad);
        Rect left = new Rect(body.x, body.y, LeftWidth, body.height);
        Rect right = new Rect(left.xMax + Pad, body.y, body.width - LeftWidth - Pad, body.height);
        Tree(left);
        List(right);
    }

    // ---- The group tree -----------------------------------------------------------------

    private void Tree(Rect rect)
    {
        Rect panel = CoreWidgets.Panel(rect, "RRP_OrganizeGroups".Translate(), FolderTex);
        Rect tools = new Rect(panel.x, panel.yMax - ToolSize, panel.width, ToolSize);
        Rect list = new Rect(panel.x, panel.y, panel.width, tools.y - 6f - panel.y);

        List<GroupInfo> groups = ReadoutGroups.All;
        float height = (groups.Count + 2) * RowHeight;
        Rect view = Theme.BeginScroll(list, ref treeScroll, height);
        try
        {
            float y = 0f;
            TreeRow(new Rect(0f, y, view.width, RowHeight), AllView, "RRP_OrganizeAll".Translate(), AllTex, 0, null);
            y += RowHeight;
            TreeRow(new Rect(0f, y, view.width, RowHeight), HiddenView, Translations.Of("RRP_OrganizeHidden", Settings.hidden.Count), HiddenTex, 0, null);
            y += RowHeight;
            foreach (GroupInfo group in groups)
            {
                TreeRow(new Rect(0f, y, view.width, RowHeight), group.key, group.label, group.icon, group.depth, group);
                y += RowHeight;
            }
        }
        finally
        {
            Theme.EndScroll();
        }

        GroupTools(tools);
    }

    private void TreeRow(Rect rect, string key, string label, Texture? icon, int depth, GroupInfo? group)
    {
        bool chosen = selected == key;
        CoreWidgets.ListRow(rect, chosen, false, Theme.HighlightBlue);
        float x = rect.x + 6f + depth * 14f;
        if (icon != null)
        {
            CoreWidgets.DrawIcon(new Rect(x, Mathf.Round(rect.center.y - 8f), 16f, 16f), icon, chosen ? Theme.TextBright : Theme.TextDim);
            x += 22f;
        }

        Color color = group != null && ReadoutData.IsHidden(group.rowKey) ? Theme.TextFaint : Theme.TextBright;
        Theme.LabelTruncated(new Rect(x, rect.y, rect.xMax - x - 4f, rect.height), label, TextAnchor.MiddleLeft, color);
        Event current = Event.current;
        if (group != null && current.type == EventType.MouseDown && current.button == 1 && Mouse.IsOver(rect))
        {
            ReadoutMenu.Open(new Row { kind = RowKind.Group, key = group.rowKey, label = group.label, icon = group.icon });
            current.Use();
        }
        else if (Widgets.ButtonInvisible(rect))
        {
            selected = key;
            listScroll = Vector2.zero;
        }
    }

    private void GroupTools(Rect rect)
    {
        GroupInfo? group = ReadoutGroups.Find(selected);
        float x = rect.x;
        if (Tool(ref x, rect, NewTex, "RRP_NewGroup", true))
        {
            Find.WindowStack.Add(new Dialog_GroupName("RRP_NewGroupTitle".Translate(), "", label => selected = ReadoutGroups.NewGroup(label)));
        }

        if (Tool(ref x, rect, SubTex, "RRP_NewSubgroup", group != null))
        {
            string parent = group!.key;
            Find.WindowStack.Add(new Dialog_GroupName("RRP_NewSubgroupTitle".Translate(group.label), "", label => selected = ReadoutGroups.NewGroup(label, parent)));
        }

        if (Tool(ref x, rect, RenameTex, "RRP_Rename", group != null))
        {
            string key = group!.key;
            Find.WindowStack.Add(new Dialog_GroupName("RRP_RenameGroup".Translate(), group.label, label => ReadoutGroups.Rename(key, label)));
        }

        if (Tool(ref x, rect, MoveTex, "RRP_MoveGroupInto", group != null))
        {
            string key = group!.key;
            ReadoutMenu.OpenGroupPicker("RRP_MoveGroupInto".Translate(), group.parent?.key ?? "", key, parent => ReadoutPanel.PlaceGroup(key, parent, LastChild(parent)), null);
        }

        if (Tool(ref x, rect, TopTex, "RRP_MoveToTop", group?.parent != null))
        {
            ReadoutPanel.PlaceGroup(group!.key, null, LastChild(null));
        }

        List<GroupInfo>? siblings = group == null ? null : group.parent?.children ?? ReadoutGroups.TopLevel;
        int index = siblings?.IndexOf(group!) ?? -1;
        if (Tool(ref x, rect, UpTex, "RRP_GroupUp", index > 0))
        {
            ReadoutPanel.PlaceGroup(group!.key, group.parent?.key, index >= 2 ? siblings![index - 2].rowKey : null);
        }

        if (Tool(ref x, rect, DownTex, "RRP_GroupDown", siblings != null && index >= 0 && index < siblings.Count - 1))
        {
            ReadoutPanel.PlaceGroup(group!.key, group.parent?.key, siblings![index + 1].rowKey);
        }

        if (Tool(ref x, rect, DeleteTex, "RRP_DeleteGroup", group != null && group.custom))
        {
            string key = group!.key;
            string label = group.label;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RRP_DeleteGroupConfirm".Translate(label), () =>
            {
                ReadoutGroups.Delete(key);
                selected = AllView;
            }, destructive: true));
        }
    }

    private static string? LastChild(string? parent)
    {
        List<GroupInfo> siblings = parent == null ? ReadoutGroups.TopLevel : ReadoutGroups.Find(parent)?.children ?? new List<GroupInfo>();
        return siblings.Count > 0 ? siblings[siblings.Count - 1].rowKey : null;
    }

    private static bool Tool(ref float x, Rect rect, Texture icon, string key, bool enabled)
    {
        Rect button = new Rect(x, rect.y, ToolSize, ToolSize);
        x += ToolSize + 4f;
        return CoreWidgets.IconButton(button, icon, Translations.Of(key), Translations.Tip(key), enabled: enabled);
    }

    // ---- The right-hand list ------------------------------------------------------------

    private void List(Rect rect)
    {
        GroupInfo? group = ReadoutGroups.Find(selected);
        string title = selected == AllView ? "RRP_OrganizeAll".Translate() : selected == HiddenView ? Translations.Of("RRP_OrganizeHidden", Settings.hidden.Count) : group?.label ?? "";
        Rect panel = CoreWidgets.Panel(rect, title, selected == AllView ? AllTex : selected == HiddenView ? HiddenTex : group?.icon);
        Rebuild();

        Rect body = panel;
        if (selected == AllView)
        {
            Rect line = new Rect(panel.x, panel.y, panel.width, SearchHeight);
            SearchField.Draw(line, ref search, SearchControl, "RRP_SearchHint".Translate());
            body.yMin = line.yMax + 6f;
        }
        else
        {
            Rect hint = new Rect(panel.x + 4f, panel.y, panel.width - 8f, 24f);
            CoreWidgets.Line(hint, (selected == HiddenView ? "RRP_OrganizeHiddenHint" : "RRP_OrganizeGroupHint").Translate(), Theme.TextDim);
            body.yMin = hint.yMax + 4f;
        }

        if (selected == HiddenView)
        {
            HiddenList(body);
        }
        else if (selected == AllView)
        {
            AllList(body);
        }
        else if (units.Count == 0)
        {
            CoreWidgets.Empty(body, "RRP_OrganizeEmptyGroup".Translate());
        }
        else
        {
            GroupList(body);
        }
    }

    private void GroupList(Rect rect)
    {
        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 1 && Mouse.IsOver(rect))
        {
            // Rows sit OrderList.RowHeight apart with RowGap between, inside its 4 px inset.
            float y = current.mousePosition.y - rect.y - OrderList<ReadoutUnit>.Padding + listScroll.y;
            int index = Mathf.FloorToInt(y / (OrderList<ReadoutUnit>.RowHeight + OrderList<ReadoutUnit>.RowGap));
            if (index >= 0 && index < units.Count && y - index * (OrderList<ReadoutUnit>.RowHeight + OrderList<ReadoutUnit>.RowGap) < OrderList<ReadoutUnit>.RowHeight)
            {
                ReadoutMenu.Open(RowOf(units[index]));
                current.Use();
            }
        }

        List<ReadoutUnit>? moved = order.Draw(rect, units, ref listScroll);
        if (moved != null)
        {
            List<string> keys = new List<string>(moved.Count);
            foreach (ReadoutUnit unit in moved)
            {
                keys.Add(unit.key);
            }

            ReadoutPanel.SaveOrder(Settings.orderGrouped, keys);
            Settings.Changed();
        }
    }

    private void AllList(Rect rect)
    {
        if (units.Count == 0)
        {
            CoreWidgets.Empty(rect, "RRP_OrganizeNoMatch".Translate());
            return;
        }

        Rect view = Theme.BeginScroll(rect, ref listScroll, units.Count * RowHeight);
        try
        {
            for (int i = 0; i < units.Count; i++)
            {
                Rect row = new Rect(0f, i * RowHeight, view.width, RowHeight);
                if (row.yMax < listScroll.y || row.y > listScroll.y + rect.height)
                {
                    continue;
                }

                ReadoutUnit unit = units[i];
                float right = PlainRow(row, i, unit.label, unit.note, IconOf(unit), ReadoutData.IsHidden(unit.key));
                Rect eye = new Rect(right - 26f, Mathf.Round(row.center.y - 13f), 26f, 26f);
                bool hidden = ReadoutData.IsHidden(unit.key);
                if (CoreWidgets.IconButton(eye, hidden ? HiddenTex : ShowTex, (hidden ? "RRP_Show" : "RRP_Hide").Translate(), "RRP_ShowHideTip".Translate()))
                {
                    ToggleHidden(unit.key);
                }

                Rect folder = new Rect(eye.x - 28f, eye.y, 26f, 26f);
                if (CoreWidgets.IconButton(folder, FolderTex, "RRP_MoveToGroup".Translate(), "RRP_MoveToGroupTip".Translate()))
                {
                    Row target = RowOf(unit);
                    ReadoutMenu.OpenGroupPicker("RRP_MoveToGroup".Translate(), unit.def != null ? ReadoutGroups.GroupOf(unit.def) : "", null,
                        key => ReadoutMenu.MoveToGroup(target, key), label => ReadoutMenu.MoveToGroup(target, ReadoutGroups.NewGroup(label)));
                }
            }
        }
        finally
        {
            Theme.EndScroll();
        }
    }

    private void HiddenList(Rect rect)
    {
        if (hiddenKeys.Count == 0)
        {
            CoreWidgets.Empty(rect, "RRP_OrganizeNothingHidden".Translate());
            return;
        }

        Rect view = Theme.BeginScroll(rect, ref listScroll, hiddenKeys.Count * RowHeight);
        try
        {
            for (int i = 0; i < hiddenKeys.Count; i++)
            {
                string key = hiddenKeys[i];
                Rect row = new Rect(0f, i * RowHeight, view.width, RowHeight);
                float right = PlainRow(row, i, ReadoutMenu.LabelOf(key), KindOf(key), IconOf(key), false);
                Rect show = new Rect(right - 80f, Mathf.Round(row.center.y - 13f), 80f, 26f);
                if (CoreWidgets.Button(show, "RRP_Show".Translate(), ShowTex))
                {
                    Settings.hidden.Remove(key);
                    Settings.Changed();
                }
            }
        }
        finally
        {
            Theme.EndScroll();
        }
    }

    // Background, icon, label and note; returns the x where buttons on the right end.
    private static float PlainRow(Rect row, int index, string label, string note, Texture? icon, bool faded)
    {
        CoreWidgets.ListRow(row, false, index % 2 == 1, Theme.HighlightGray);
        float alpha = faded ? 0.45f : 1f;
        if (icon != null)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            CoreWidgets.DrawIcon(new Rect(row.x + 6f, Mathf.Round(row.center.y - 11f), 22f, 22f), icon);
            GUI.color = Color.white;
        }

        float labelWidth = Mathf.Min(CoreWidgets.Width(label) + 4f, row.width * 0.5f);
        CoreWidgets.Line(new Rect(row.x + 34f, row.y, labelWidth, row.height), label, Faces.Fade(Theme.TextBright, alpha));
        CoreWidgets.Line(new Rect(row.x + 34f + labelWidth + 8f, row.y, row.width - labelWidth - 34f - 8f - 120f, row.height), note, Theme.TextFaint);
        return row.xMax - 4f;
    }

    // ---- Building the lists -------------------------------------------------------------

    private void Rebuild()
    {
        if (builtFor == Settings.version && builtView == selected && builtSearch == search)
        {
            // A group's counts follow the map without rebuilding the list, so a drag in progress isn't disturbed.
            if (notesFor != ReadoutCounts.Stamp && selected != HiddenView && selected != AllView)
            {
                CountNotes(Find.CurrentMap);
            }

            return;
        }

        builtFor = Settings.version;
        builtView = selected;
        builtSearch = search;
        units.Clear();
        hiddenKeys.Clear();
        Map? map = Find.CurrentMap;

        if (selected == HiddenView)
        {
            hiddenKeys.AddRange(Settings.hidden);
            hiddenKeys.SortBy(key => ReadoutMenu.LabelOf(key));
            return;
        }

        if (selected == AllView)
        {
            foreach (ThingDef def in Countable())
            {
                if (search.NullOrEmpty() || def.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ReadoutUnit unit = new ReadoutUnit { key = Keys.Of(def), def = def, label = def.LabelCap };
                    GroupInfo? group = ReadoutGroups.Find(ReadoutGroups.GroupOf(def));
                    unit.note = group?.label ?? "";
                    units.Add(unit);
                }
            }

            units.SortBy(unit => unit.label);
            return;
        }

        List<ThingDef> defs = new List<ThingDef>();
        foreach (ThingDef def in BaseOrder(map))
        {
            if (ReadoutGroups.GroupOf(def) == selected)
            {
                defs.Add(def);
            }
        }

        units.AddRange(ReadoutData.UnitsOf(defs, ReadoutMode.Grouped, withPinned: true));
        foreach (ReadoutUnit unit in units)
        {
            unit.label = unit.def?.LabelCap ?? unit.family?.LabelCap ?? unit.key;
        }

        CountNotes(map);
    }

    // Each unit's count beside its name in a group's list.
    private void CountNotes(Map? map)
    {
        notesFor = ReadoutCounts.Stamp;
        foreach (ReadoutUnit unit in units)
        {
            if (map != null)
            {
                int count = 0;
                if (unit.def != null)
                {
                    count = ReadoutCounts.Count(map, unit.def);
                }
                else
                {
                    foreach (ThingDef member in unit.members)
                    {
                        count += ReadoutCounts.Count(map, member);
                    }
                }

                unit.note = unit.def == null ? "RRP_OrganizeFamilyNote".Translate(unit.members.Count, count.ToStringCached()) : count.ToStringCached();
            }
        }
    }

    // The base game's counting order when a map is open, else every countable thing.
    private static IEnumerable<ThingDef> BaseOrder(Map? map)
    {
        if (map != null)
        {
            return map.resourceCounter.AllCountedAmounts.Keys;
        }

        return Countable();
    }

    private static List<ThingDef> Countable()
    {
        if (countable == null)
        {
            countable = new List<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.CountAsResource && def.PlayerAcquirable)
                {
                    countable.Add(def);
                }
            }
        }

        return countable;
    }

    // ---- Helpers ------------------------------------------------------------------------

    private static void ToggleHidden(string key)
    {
        if (!Settings.hidden.Remove(key))
        {
            Settings.hidden.Add(key);
            Settings.pinned.Remove(key);
        }

        Settings.Changed();
    }

    private static Row RowOf(ReadoutUnit unit)
    {
        return new Row
        {
            kind = unit.def != null ? RowKind.Item : RowKind.Family,
            key = unit.key,
            label = unit.label,
            def = unit.def ?? (unit.members.Count > 0 ? unit.members[0] : null),
            category = unit.family,
            members = unit.def != null ? null : new List<ThingDef>(unit.members),
        };
    }

    private static Texture? IconOf(ReadoutUnit unit)
    {
        ThingDef? def = unit.def ?? (unit.members.Count > 0 ? unit.members[0] : null);
        return def?.uiIcon;
    }

    private static Texture? IconOf(string key)
    {
        if (Keys.ThingOf(key) is ThingDef thing)
        {
            return thing.uiIcon;
        }

        if (Keys.GroupOf(key) is string group)
        {
            return ReadoutGroups.Find(group)?.icon;
        }

        return Keys.CategoryOf(key)?.icon;
    }

    private static string KindOf(string key)
    {
        string kind = key.StartsWith(Keys.Group) ? "RRP_KindGroup" : key.StartsWith(Keys.Category) ? "RRP_KindCategory" : key.StartsWith(Keys.Family) ? "RRP_KindFamily" : "RRP_KindThing";
        return kind.Translate();
    }
}
