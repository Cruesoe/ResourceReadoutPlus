using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Sets or clears the minimum stock for a row; below it the row turns red and the low-stock alert lists it.</summary>
[StaticConstructorOnStartup]
public class Dialog_MinimumStock : ThemedPopup
{
    private const float ButtonHeight = 32f;
    private const float FieldHeight = 30f;
    private const float HintHeight = 44f;
    private const int MostDigits = 7;
    private const string Field = "RRP_Minimum";

    private static readonly Texture2D MinimumTex = CoreWidgets.Icon("warning");
    private static readonly Texture2D SetTex = CoreWidgets.Icon("check");
    private static readonly Texture2D ClearTex = CoreWidgets.Icon("x");

    private readonly string key;
    private readonly string title;
    private string buffer;

    public Dialog_MinimumStock(string key, string label)
    {
        this.key = key;
        title = "RRP_MinimumTitle".Translate(label);
        int current = ReadoutMod.Settings.minimums.TryGetValue(key, out int saved) ? saved : 0;
        buffer = current > 0 ? current.ToString() : "";
        AsDialog();
        forcePause = false;
    }

    public override Vector2 InitialSize => new Vector2(380f, 220f);

    protected override string Part => "Resource Readout+ minimum stock";

    protected override void Contents(Rect inRect)
    {
        Rect body = TitleBar(inRect, MinimumTex, title, null, null);
        Rect hint = new Rect(body.x, body.y, body.width, HintHeight);
        Theme.Label(hint, "RRP_MinimumHint".Translate(), TextAnchor.UpperLeft, Theme.TextDim);

        Rect field = new Rect(body.x, hint.yMax + 4f, body.width, FieldHeight);
        if (SearchField.TextBox(field, ref buffer, Field, "0", MostDigits))
        {
            buffer = Digits(buffer);
        }

        if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != Field)
        {
            UI.FocusControl(Field, this);
        }

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        float half = (body.width - 10f) / 2f;
        Rect clear = new Rect(body.x, body.yMax - ButtonHeight, half, ButtonHeight);
        Rect set = new Rect(clear.xMax + 10f, clear.y, half, ButtonHeight);
        if (CoreWidgets.Button(clear, "RRP_MinimumClear".Translate(), ClearTex, enabled: ReadoutMod.Settings.minimums.ContainsKey(key), tip: "RRP_MinimumClearTip".Translate()))
        {
            ReadoutMod.Settings.minimums.Remove(key);
            ReadoutMod.Settings.Changed();
            Close();
        }

        if (CoreWidgets.Button(set, "RRP_MinimumSet".Translate(), SetTex) || enter)
        {
            int value = int.TryParse(buffer, out int typed) ? typed : 0;
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
    }

    // Only the digits of what was typed.
    private static string Digits(string text)
    {
        System.Text.StringBuilder digits = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c >= '0' && c <= '9')
            {
                digits.Append(c);
            }
        }

        return digits.ToString();
    }
}
