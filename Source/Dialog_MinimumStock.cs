using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Sets or clears the minimum stock for a row; below it the row turns red and the low-stock alert lists it.</summary>
public class Dialog_MinimumStock : Window
{
    private const float ButtonHeight = 32f;
    private const int Most = 9999999;

    private readonly string key;
    private readonly string label;
    private int value;
    private string buffer;

    public Dialog_MinimumStock(string key, string label)
    {
        this.key = key;
        this.label = label;
        value = ReadoutMod.Settings.minimums.TryGetValue(key, out int current) ? current : 0;
        buffer = value > 0 ? value.ToString() : "";
        forcePause = false;
        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new Vector2(380f, 190f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 30f, 34f), "RRP_MinimumTitle".Translate(label));
        Text.Font = GameFont.Small;

        Rect hint = new Rect(inRect.x, inRect.y + 38f, inRect.width, 44f);
        Widgets.Label(hint, "RRP_MinimumHint".Translate());

        Rect field = new Rect(inRect.x, hint.yMax + 4f, inRect.width, 30f);
        GUI.SetNextControlName("RRP_Minimum");
        Widgets.TextFieldNumeric(field, ref value, ref buffer, 0, Most);
        if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "RRP_Minimum")
        {
            UI.FocusControl("RRP_Minimum", this);
        }

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        float half = (inRect.width - 10f) / 2f;
        Rect clear = new Rect(inRect.x, inRect.yMax - ButtonHeight, half, ButtonHeight);
        Rect set = new Rect(clear.xMax + 10f, clear.y, half, ButtonHeight);
        if (Widgets.ButtonText(clear, "RRP_MinimumClear".Translate(), active: ReadoutMod.Settings.minimums.ContainsKey(key)))
        {
            ReadoutMod.Settings.minimums.Remove(key);
            ReadoutMod.Settings.Changed();
            Close();
        }

        if (Widgets.ButtonText(set, "RRP_MinimumSet".Translate()) || enter)
        {
            if (value > 0)
            {
                ReadoutMod.Settings.minimums[key] = value;
            }
            else
            {
                ReadoutMod.Settings.minimums.Remove(key);
            }

            ReadoutMod.Settings.Changed();
            if (enter)
            {
                Event.current.Use();
            }

            Close();
        }

        CoreWidgets.Tip(clear, "RRP_MinimumClear".Translate(), "RRP_MinimumClearTip".Translate());
    }
}
