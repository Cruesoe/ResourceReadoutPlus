using System.Text;
using RimWorld;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Lists every row below its minimum stock on the current map.</summary>
public class Alert_LowStock : Alert
{
    private readonly StringBuilder text = new StringBuilder();
    private int textFor = -1;
    private string explanation = "";

    public Alert_LowStock()
    {
        defaultPriority = AlertPriority.Medium;
    }

    public override AlertReport GetReport()
    {
        if (!ReadoutMod.Settings.lowStockAlert || ReadoutMod.Settings.minimums.Count == 0 || Find.CurrentMap is not Map map)
        {
            return false;
        }

        ReadoutData.Ensure(map);
        return ReadoutData.Low.Count > 0;
    }

    public override string GetLabel()
    {
        return "RRP_LowStockAlert".Translate(ReadoutData.Low.Count);
    }

    public override TaggedString GetExplanation()
    {
        if (textFor != ReadoutData.Version)
        {
            textFor = ReadoutData.Version;
            text.Clear();
            text.AppendLine("RRP_LowStockAlertDesc".Translate());
            text.AppendLine();
            foreach (LowStock low in ReadoutData.Low)
            {
                text.AppendLine("  - " + "RRP_LowStockLine".Translate(low.label, low.count.ToStringCached(), low.minimum.ToStringCached()));
            }

            explanation = text.ToString().TrimEndNewlines();
        }

        return explanation;
    }
}
