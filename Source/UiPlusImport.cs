using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Brings over Vanilla UI+'s resource readout settings by reading its settings file; Vanilla UI+ itself is never loaded or referenced.</summary>
public static class UiPlusImport
{
    private const string UiPlusPackageId = "cruesoe.vanillauiplus";
    private const string UiPlusHandle = "UiPlusMod";

    // Vanilla UI+'s settings file, whether or not it is active; null when there is none.
    public static string? SettingsFile()
    {
        string? folder = null;
        foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
        {
            if (string.Equals(mod.PackageIdPlayerFacing, UiPlusPackageId, StringComparison.OrdinalIgnoreCase))
            {
                folder = mod.FolderName;
                break;
            }
        }

        folder ??= ModLister.GetModWithIdentifier(UiPlusPackageId, ignorePostfix: true)?.FolderName;
        if (folder == null)
        {
            return null;
        }

        string path = Path.Combine(GenFilePaths.ConfigFolderPath, GenText.SanitizeFilename($"Mod_{folder}_{UiPlusHandle}.xml"));
        return File.Exists(path) ? path : null;
    }

    // Copies the resource order, moves, hidden rows, count-all rows, open categories and zero-count choice; true when anything came over.
    public static bool Run()
    {
        string? path = SettingsFile();
        if (path == null)
        {
            return false;
        }

        try
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            ReadoutSettings settings = ReadoutMod.Settings;
            bool any = false;
            any |= Copy(doc, "resourceOrderSimple", settings.orderSimple, s => s);
            any |= Copy(doc, "resourceOrderCategorized", settings.orderCategorized, s => s);
            any |= Copy(doc, "resourceHidden", settings.hidden, s => s);
            any |= Copy(doc, "resourceCountAll", settings.countAll, s => s);
            any |= Copy(doc, "resourceOpenCategories", settings.openCategories, s => Keys.Category + s);
            any |= CopyParents(doc, settings.parentOf);
            if (Bool(doc, "showZeroResources") is bool zero)
            {
                settings.showZero = zero;
            }

            if (Bool(doc, "countHiddenInTotals") is bool hidden)
            {
                settings.countHiddenInTotals = hidden;
            }

            settings.Changed();
            return any;
        }
        catch (Exception exception)
        {
            Log.Warning("[Resource Readout+] Couldn't read Vanilla UI+'s resource settings from " + path + "; nothing was brought over.\n" + exception);
            return false;
        }
    }

    private static bool Copy(XmlDocument doc, string name, List<string> into, Func<string, string> convert)
    {
        XmlNode? node = doc.SelectSingleNode("//" + name);
        if (node == null)
        {
            return false;
        }

        bool any = false;
        foreach (XmlNode li in node.ChildNodes)
        {
            string value = convert(li.InnerText.Trim());
            if (li.NodeType == XmlNodeType.Element && value.Length > 0 && !into.Contains(value))
            {
                into.Add(value);
                any = true;
            }
        }

        return any;
    }

    private static bool CopyParents(XmlDocument doc, Dictionary<string, string> into)
    {
        XmlNode? keys = doc.SelectSingleNode("//resourceParents/keys");
        XmlNode? values = doc.SelectSingleNode("//resourceParents/values");
        if (keys == null || values == null || keys.ChildNodes.Count != values.ChildNodes.Count)
        {
            return false;
        }

        bool any = false;
        for (int i = 0; i < keys.ChildNodes.Count; i++)
        {
            string key = keys.ChildNodes[i]?.InnerText.Trim() ?? "";
            string value = values.ChildNodes[i]?.InnerText.Trim() ?? "";
            if (key.Length > 0 && value.Length > 0 && !into.ContainsKey(key))
            {
                into[key] = value;
                any = true;
            }
        }

        return any;
    }

    private static bool? Bool(XmlDocument doc, string name)
    {
        XmlNode? node = doc.SelectSingleNode("//" + name);
        return node != null && bool.TryParse(node.InnerText.Trim(), out bool value) ? value : null;
    }
}
