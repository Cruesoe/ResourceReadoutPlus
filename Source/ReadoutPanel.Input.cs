using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ResourceReadoutPlus;

// Clicks, right-click menus and dragging rows to a new place.
public static partial class ReadoutPanel
{
    private static Row? pressRow;
    private static Vector2 pressAt;
    private static bool dragging;

    private static void ResetPress()
    {
        pressRow = null;
        dragging = false;
    }

    private static void RowInput(Row row, Rect rect)
    {
        Event current = Event.current;
        if (current.type != EventType.MouseDown || !Mouse.IsOver(rect))
        {
            return;
        }

        if (current.button == 0)
        {
            pressRow = row;
            pressAt = current.mousePosition;
            dragging = false;
            current.Use();
        }
        else if (current.button == 1)
        {
            ResetPress();
            ReadoutMenu.Open(row);
            current.Use();
        }
    }

    // Runs inside the scroll view, so mouse positions are in row space.
    private static void DragInput(List<Row> rows, float width)
    {
        if (pressRow == null)
        {
            return;
        }

        Event current = Event.current;
        if (current.type == EventType.Repaint && !ListDrag.Held)
        {
            ResetPress();
            return;
        }

        if (current.type == EventType.MouseDrag)
        {
            if (!dragging && pressRow.draggable && ReadoutData.search.NullOrEmpty() && ListDrag.PastStart(pressAt, current.mousePosition))
            {
                dragging = true;
            }

            if (dragging)
            {
                current.Use();
            }
        }
        else if (current.rawType == EventType.MouseUp && current.button == 0)
        {
            Row pressed = pressRow;
            bool wasDragging = dragging;
            ResetPress();
            if (wasDragging)
            {
                Drop(rows, pressed, current.mousePosition.y);
            }
            else
            {
                int index = Mathf.FloorToInt(current.mousePosition.y / RowHeight);
                // Compared by key: the rows may have been rebuilt since the press.
                if (index >= 0 && index < rows.Count && rows[index].key == pressed.key)
                {
                    Click(pressed);
                }
            }

            current.Use();
        }
        else if (current.type == EventType.Repaint && dragging)
        {
            int slot = DropSlot(rows, current.mousePosition.y);
            Theme.Fill(new Rect(0f, slot * RowHeight - 1f, width, 2f), Theme.HighlightYellow);
        }
    }

    private static int DropSlot(List<Row> rows, float y)
    {
        return Mathf.Clamp(Mathf.RoundToInt(y / RowHeight), 0, rows.Count);
    }

    private static void Click(Row row)
    {
        if (row.kind == RowKind.Item && row.def != null && row.mark != ResourceDiscoveries.None)
        {
            ResourceDiscoveries.Current?.Clear(Find.CurrentMap, row.def);
        }

        // The filter shows everything inside headers and families open, so clicking them does nothing while it is on.
        if (Settings.Filtering && row.kind != RowKind.Item)
        {
            return;
        }

        switch (row.kind)
        {
            case RowKind.Group:
                Toggle(Settings.foldedGroups, row.key);
                break;
            case RowKind.Category:
                Toggle(Settings.openCategories, row.key);
                break;
            case RowKind.Family:
                Toggle(Settings.openFamilies, row.key);
                break;
            case RowKind.Item when row.def != null:
                ReadoutJump.Next(Find.CurrentMap, row.def);
                return;
            default:
                return;
        }

        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        Settings.Changed();
    }

    private static void Toggle(List<string> list, string key)
    {
        if (!list.Remove(key))
        {
            list.Add(key);
        }
    }

    // ---- Dropping -----------------------------------------------------------------------

    private static void Drop(List<Row> rows, Row dragged, float y)
    {
        int slot = DropSlot(rows, y);
        if (dragged.kind == RowKind.Group)
        {
            DropGroup(rows, dragged, slot);
            return;
        }

        Row? above = slot > 0 ? rows[slot - 1] : null;
        Row? below = slot < rows.Count ? rows[slot] : null;
        if (above == dragged || below == dragged)
        {
            return;
        }

        string targetList;
        string? after;
        if (above == null)
        {
            targetList = below?.list ?? dragged.list;
            after = null;
        }
        else if (above.opens != null && above.open)
        {
            targetList = above.opens;
            after = null;
        }
        else if (above.kind == RowKind.Group && above.opens != null)
        {
            // A folded group takes the row at its end.
            targetList = above.opens;
            after = ReadoutData.ListUnits.TryGetValue(targetList, out List<string>? units) && units.Count > 0 ? units[units.Count - 1] : null;
        }
        else
        {
            targetList = above.list;
            after = above.unit;
        }

        if (targetList.NullOrEmpty() || targetList == dragged.unit || after == dragged.unit)
        {
            return;
        }

        Move(dragged, targetList, after);
    }

    // A group dropped below an open group's header goes inside it; below a folded header, beside it; below a thing, into that thing's group.
    private static void DropGroup(List<Row> rows, Row dragged, int slot)
    {
        string? key = Keys.GroupOf(dragged.key);
        if (key == null)
        {
            return;
        }

        Row? above = slot > 0 ? rows[slot - 1] : null;
        string? parent;
        string? after;
        if (above == null || above.list == Keys.PinnedList)
        {
            parent = null;
            after = null;
        }
        else if (above.kind == RowKind.Group && Keys.GroupOf(above.key) is string aboveGroup)
        {
            if (above.open && above.key != dragged.key)
            {
                parent = aboveGroup;
                after = null;
            }
            else
            {
                parent = ReadoutGroups.Find(aboveGroup)?.parent?.key;
                after = above.unit;
            }
        }
        else if (Keys.GroupOf(above.list) is string thingGroup)
        {
            parent = thingGroup;
            GroupInfo? info = ReadoutGroups.Find(thingGroup);
            after = info != null && info.children.Count > 0 ? info.children[info.children.Count - 1].rowKey : null;
        }
        else
        {
            return;
        }

        PlaceGroup(key, parent, after);
    }

    // Puts a group under parent (null for the top level), after a sibling (null for first).
    public static void PlaceGroup(string key, string? parent, string? after)
    {
        string rowKey = Keys.OfGroup(key);
        if (after == rowKey)
        {
            return;
        }

        if (!ReadoutGroups.SetParent(key, parent))
        {
            Messages.Message("RRP_MoveGroupIntoItself".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        List<GroupInfo> siblings = parent == null ? ReadoutGroups.TopLevel : ReadoutGroups.Find(parent)?.children ?? new List<GroupInfo>();
        List<string> order = new List<string>(siblings.Count + 1);
        foreach (GroupInfo sibling in siblings)
        {
            order.Add(sibling.rowKey);
        }

        Place(order, rowKey, after);
        SaveOrder(Settings.groupOrder, order);
        Settings.Changed();
    }

    // The group before this one among its siblings, which Demote moves it into.
    public static GroupInfo? GroupAbove(GroupInfo group)
    {
        List<GroupInfo> siblings = group.parent?.children ?? ReadoutGroups.TopLevel;
        int index = siblings.IndexOf(group);
        return index > 0 ? siblings[index - 1] : null;
    }

    // Moves a group out of its parent, placing it just below the parent.
    public static void Promote(GroupInfo group)
    {
        if (group.parent is GroupInfo parent)
        {
            PlaceGroup(group.key, parent.parent?.key, parent.rowKey);
        }
    }

    // Moves a group to the end of the group above it.
    public static void Demote(GroupInfo group)
    {
        if (GroupAbove(group) is GroupInfo above)
        {
            List<GroupInfo> children = above.children;
            PlaceGroup(group.key, above.key, children.Count > 0 ? children[children.Count - 1].rowKey : null);
        }
    }

    private static void Move(Row dragged, string targetList, string? after)
    {
        if (targetList == Keys.PinnedList)
        {
            if (dragged.kind != RowKind.Item && dragged.kind != RowKind.Family)
            {
                return;
            }

            Place(Settings.pinned, dragged.unit, after);
            Settings.Changed();
            return;
        }

        if (dragged.list == Keys.PinnedList)
        {
            Settings.pinned.Remove(dragged.unit);
        }

        bool moved = Settings.mode switch
        {
            ReadoutMode.Grouped => MoveGrouped(dragged, targetList),
            ReadoutMode.Categorized => MoveCategorized(dragged, targetList),
            _ => targetList == Keys.SimpleList,
        };

        if (moved)
        {
            List<string> order = new List<string>(ReadoutData.ListUnits.TryGetValue(targetList, out List<string>? units) ? units : new List<string>());
            Place(order, dragged.unit, after);
            SaveOrder(Settings.OrderFor(Settings.mode), order);
        }

        Settings.Changed();
    }

    private static bool MoveGrouped(Row dragged, string targetList)
    {
        if (Keys.GroupOf(targetList) is not string group)
        {
            return false;
        }

        if (dragged.kind == RowKind.Family && dragged.members != null)
        {
            foreach (ThingDef member in dragged.members)
            {
                SetGroup(member, group);
            }
        }
        else if (dragged.def != null)
        {
            SetGroup(dragged.def, group);
        }

        return true;
    }

    public static void SetGroup(ThingDef def, string group)
    {
        string key = Keys.Of(def);
        if (group == ReadoutGroups.BuiltinGroupOf(def))
        {
            Settings.groupOf.Remove(key);
        }
        else
        {
            Settings.groupOf[key] = group;
        }
    }

    private static bool MoveCategorized(Row dragged, string targetList)
    {
        Def? def = dragged.kind == RowKind.Category ? dragged.category : dragged.def;
        if (def == null)
        {
            return false;
        }

        ThingCategoryDef? parent = targetList == Keys.Top ? null : Keys.CategoryOf(targetList);
        if (targetList != Keys.Top && parent == null)
        {
            return false;
        }

        if (def is ThingCategoryDef category && ReadoutData.IsWithin(parent, category))
        {
            Messages.Message("RRP_MoveIntoItself".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        if (ReadoutData.IsUsualParent(def, parent))
        {
            Settings.parentOf.Remove(dragged.key);
        }
        else
        {
            Settings.parentOf[dragged.key] = parent == null ? Keys.Top : targetList;
        }

        return true;
    }

    private static void Place(List<string> order, string unit, string? after)
    {
        order.Remove(unit);
        int index = after == null ? 0 : order.IndexOf(after) + 1;
        if (after != null && index == 0)
        {
            index = order.Count;
        }

        order.Insert(index, unit);
    }

    // Ranks only compare rows in the same list, so the list is saved again as one block at the end.
    public static void SaveOrder(List<string> saved, List<string> order)
    {
        HashSet<string> keys = new HashSet<string>(order);
        saved.RemoveAll(keys.Contains);
        saved.AddRange(order);
    }
}
