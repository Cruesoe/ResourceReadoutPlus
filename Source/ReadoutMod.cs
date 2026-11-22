using System;
using HarmonyLib;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResourceReadoutPlus;

public class ReadoutMod : Mod
{
    public static ReadoutSettings Settings = null!;
    public static ReadoutMod? Instance;

    // Categories combined into one row until the player changes them.
    public static readonly string[] UsualFamilies = { "Leathers", "Wools", "StoneBlocks", "Fish", "EggsFertilized", "EggsUnfertilized" };

    public ReadoutMod(ModContentPack content)
        : base(content)
    {
        Instance = this;
        Settings = GetSettings<ReadoutSettings>();
        WindowHooks.BeforeWindows(() => Settings.FlushIfDue());
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
        // New usual families are added once, keeping any the player separated before.
        if (!Settings.familiesSet || Settings.familiesVersion < ReadoutSettings.CurrentFamilies)
        {
            foreach (string family in UsualFamilies)
            {
                if (!Settings.familiesSet || (Settings.familiesVersion < 2 && (family == "Fish" || family.StartsWith("Eggs"))))
                {
                    if (!Settings.families.Contains(family))
                    {
                        Settings.families.Add(family);
                    }
                }
            }

            Settings.familiesSet = true;
            Settings.familiesVersion = ReadoutSettings.CurrentFamilies;
            changed = true;
        }

        if (changed)
        {
            Settings.Changed();
        }
    }
}
