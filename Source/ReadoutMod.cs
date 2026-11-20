using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

public class ReadoutMod : Mod
{
    public static ReadoutSettings Settings = null!;
    public static ReadoutMod? Instance;

    // Categories combined into one row until the player changes them.
    public static readonly string[] UsualFamilies = { "Leathers", "Wools", "StoneBlocks" };

    public ReadoutMod(ModContentPack content)
        : base(content)
    {
        Instance = this;
        Settings = GetSettings<ReadoutSettings>();
        Patch();
        LongEventHandler.ExecuteWhenFinished(Startup);
    }

    public override string SettingsCategory()
    {
        return "RRP_SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        SettingsPage.Draw(inRect);
    }

    // Class by class, so one failing patch is reported on its own.
    private static void Patch()
    {
        Harmony harmony = new Harmony(ReadoutPanel.PackageId);
        foreach (Type type in typeof(ReadoutMod).Assembly.GetTypes())
        {
            if (!type.IsDefined(typeof(HarmonyAttribute), inherit: false))
            {
                continue;
            }

            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception exception)
            {
                Log.Error($"[Resource Readout+] Could not apply {type.Name}.\n{exception}");
            }
        }
    }

    private static void Startup()
    {
        bool changed = false;
        if (!Settings.familiesSet)
        {
            Settings.familiesSet = true;
            Settings.families.AddRange(UsualFamilies);
            changed = true;
        }

        if (!Settings.importedUiPlus)
        {
            Settings.importedUiPlus = true;
            UiPlusImport.Run();
            changed = true;
        }

        if (changed)
        {
            Settings.Changed();
        }
    }
}
