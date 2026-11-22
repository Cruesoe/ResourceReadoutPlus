using System;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Asks for a group's name, for a new group or a rename.</summary>
[StaticConstructorOnStartup]
public class Dialog_GroupName : ThemedPopup
{
    private const int MaxLength = 32;
    private const float ButtonHeight = 32f;
    private const float FieldHeight = 30f;
    private const string Field = "RRP_GroupName";

    private static readonly Texture2D FolderTex = CoreWidgets.Icon("folder");
    private static readonly Texture2D OkTex = CoreWidgets.Icon("check");

    private readonly string title;
    private readonly Action<string> accept;
    private string name;

    public Dialog_GroupName(string title, string current, Action<string> accept)
    {
        this.title = title;
        this.accept = accept;
        name = current;
        AsDialog();
        forcePause = false;
    }

    public override Vector2 InitialSize => new Vector2(380f, 170f);

    protected override string Part => "Resource Readout+ group name";

    protected override void Contents(Rect inRect)
    {
        Rect body = TitleBar(inRect, FolderTex, title, null, null);
        Rect field = new Rect(body.x, body.y, body.width, FieldHeight);
        SearchField.TextBox(field, ref name, Field, "", MaxLength);
        if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != Field)
        {
            UI.FocusControl(Field, this);
        }

        bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        Rect ok = new Rect(body.xMax - 140f, body.yMax - ButtonHeight, 140f, ButtonHeight);
        bool valid = !name.Trim().NullOrEmpty();
        if ((CoreWidgets.Button(ok, "OK".Translate(), OkTex, enabled: valid) || enter) && valid)
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
