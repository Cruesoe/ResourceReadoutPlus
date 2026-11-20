using System;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Asks for a group's name, for a new group or a rename.</summary>
public class Dialog_GroupName : Window
{
    private const int MaxLength = 32;
    private const float ButtonHeight = 32f;

    private readonly string title;
    private readonly Action<string> accept;
    private string name;

    public Dialog_GroupName(string title, string current, Action<string> accept)
    {
        this.title = title;
        this.accept = accept;
        name = current;
        forcePause = false;
        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new Vector2(380f, 160f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 30f, 34f), title);
        Text.Font = GameFont.Small;

        Rect field = new Rect(inRect.x, inRect.y + 44f, inRect.width, 30f);
        GUI.SetNextControlName("RRP_GroupName");
        string typed = Widgets.TextField(field, name);
        if (typed.Length <= MaxLength)
        {
            name = typed;
        }

        if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "RRP_GroupName")
        {
            UI.FocusControl("RRP_GroupName", this);
        }

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        Rect ok = new Rect(inRect.xMax - 140f, inRect.yMax - ButtonHeight, 140f, ButtonHeight);
        bool valid = !name.Trim().NullOrEmpty();
        if ((Widgets.ButtonText(ok, "OK".Translate(), active: valid) || enter) && valid)
        {
            accept(name.Trim());
            if (enter)
            {
                Event.current.Use();
            }

            Close();
        }
    }
}
