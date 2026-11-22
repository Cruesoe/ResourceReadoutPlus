using System.Collections.Generic;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>Translated text cached per language, so draw code doesn't build strings every frame. Lifetime: session, cleared when the language changes.</summary>
public static class Translations
{
    private static readonly Dictionary<string, string> Plain = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> Tips = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> Capitals = new Dictionary<string, string>();
    private static readonly Dictionary<(string, int), string> WithNumber = new Dictionary<(string, int), string>();
    private static readonly Dictionary<(string, string), string> WithText = new Dictionary<(string, string), string>();
    private static LoadedLanguage? language;

    public static string Of(string key)
    {
        Check();
        if (!Plain.TryGetValue(key, out string? text))
        {
            text = key.Translate();
            Plain[key] = text;
        }

        return text;
    }

    // The text under "<key>Tip".
    public static string Tip(string key)
    {
        Check();
        if (!Tips.TryGetValue(key, out string? text))
        {
            text = (key + "Tip").Translate();
            Tips[key] = text;
        }

        return text;
    }

    // The text with its first letter capitalized.
    public static string Capital(string key)
    {
        Check();
        if (!Capitals.TryGetValue(key, out string? text))
        {
            text = key.Translate().CapitalizeFirst();
            Capitals[key] = text;
        }

        return text;
    }

    public static string Of(string key, int number)
    {
        Check();
        if (!WithNumber.TryGetValue((key, number), out string? text))
        {
            text = key.Translate(number);
            WithNumber[(key, number)] = text;
        }

        return text;
    }

    public static string Of(string key, string argument)
    {
        Check();
        if (!WithText.TryGetValue((key, argument), out string? text))
        {
            text = key.Translate(argument);
            WithText[(key, argument)] = text;
        }

        return text;
    }

    private static void Check()
    {
        if (language == LanguageDatabase.activeLanguage)
        {
            return;
        }

        language = LanguageDatabase.activeLanguage;
        Plain.Clear();
        Tips.Clear();
        Capitals.Clear();
        WithNumber.Clear();
        WithText.Clear();
    }
}
