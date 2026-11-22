using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ResourceReadoutPlus;

/// <summary>
/// Resources the colony has stored this game, and the new and back-in-stock marks on readout rows; saved with the game.
/// Lifetime: game (one instance per game, found through Current).
/// </summary>
public class ResourceDiscoveries : GameComponent
{
    public const int None = 0;
    public const int New = 1;
    public const int Back = 2;

    // A resource counts as back in stock only after this long at zero, so everyday ups and downs aren't marked.
    public const int OutFor = GenDate.TicksPerDay;
    private const int SettleTime = GenDate.TicksPerDay;
    private const int TidyEvery = GenDate.TicksPerHour;

    public static ResourceDiscoveries? Current;

    // Bumped when a mark is added or removed; the readout rebuilds.
    public static int Version;

    private sealed class MapState
    {
        public readonly HashSet<ThingDef> stocked = new HashSet<ThingDef>();
        public readonly Dictionary<ThingDef, int> outSince = new Dictionary<ThingDef, int>();
        public readonly Dictionary<ThingDef, int> markTick = new Dictionary<ThingDef, int>();
        public readonly Dictionary<ThingDef, int> markKind = new Dictionary<ThingDef, int>();
    }

    // Every resource ever stored on any home map of this game.
    private readonly HashSet<ThingDef> seen = new HashSet<ThingDef>();
    private readonly Dictionary<int, MapState> maps = new Dictionary<int, MapState>();

    // Saved forms, filled only while saving or loading.
    private List<string>? savedSeen;
    private List<string>? savedStocked;
    private Dictionary<string, int>? savedOut;
    private Dictionary<string, int>? savedMarkTick;
    private Dictionary<string, int>? savedMarkKind;

    private static ReadoutSettings Settings => ReadoutMod.Settings;

    public ResourceDiscoveries(Game game)
    {
        Current = this;
    }

    // Called after each base-game count update. A map's first update takes its stock as already seen, so new games and old saves start unmarked.
    public void Update(Map map)
    {
        if (!map.IsPlayerHome)
        {
            return;
        }

        bool first = !maps.TryGetValue(map.uniqueID, out MapState? state);
        if (state == null)
        {
            state = new MapState();
            maps[map.uniqueID] = state;
        }

        int now = Find.TickManager.TicksGame;

        // A new map's first day of hauling (a new colony's starting things) is taken as seen too.
        first |= now - map.generationTick < SettleTime;
        foreach (KeyValuePair<ThingDef, int> entry in map.resourceCounter.AllCountedAmounts)
        {
            ThingDef def = entry.Key;
            if (entry.Value > 0)
            {
                bool isNew = seen.Add(def) && !first;
                if (!state.stocked.Add(def))
                {
                    continue;
                }

                if (isNew)
                {
                    Mark(state, def, New, now);
                }
                else if (state.outSince.TryGetValue(def, out int since))
                {
                    state.outSince.Remove(def);
                    if (!first && now - since >= OutFor)
                    {
                        Mark(state, def, Back, now);
                    }
                }
            }
            else if (state.stocked.Remove(def))
            {
                state.outSince[def] = now;
            }
        }
    }

    private static void Mark(MapState state, ThingDef def, int kind, int now)
    {
        state.markTick[def] = now;
        state.markKind[def] = kind;
        Version++;
    }

    // The mark shown on a row: None when there is none, it ran out of time or its kind is turned off.
    public int MarkOf(Map map, ThingDef def)
    {
        if (!Settings.newMarks || !maps.TryGetValue(map.uniqueID, out MapState? state) || !state.markKind.TryGetValue(def, out int kind))
        {
            return None;
        }

        if (kind == Back && !Settings.backInStock)
        {
            return None;
        }

        return Find.TickManager.TicksGame - state.markTick[def] < Settings.newHours * GenDate.TicksPerHour ? kind : None;
    }

    public bool AnyMarks(Map map)
    {
        if (!maps.TryGetValue(map.uniqueID, out MapState? state))
        {
            return false;
        }

        foreach (ThingDef def in state.markKind.Keys)
        {
            if (MarkOf(map, def) != None)
            {
                return true;
            }
        }

        return false;
    }

    public void Clear(Map map, ThingDef def)
    {
        if (maps.TryGetValue(map.uniqueID, out MapState? state) && state.markKind.Remove(def))
        {
            state.markTick.Remove(def);
            Version++;
        }
    }

    public void ClearAll(Map map)
    {
        if (maps.TryGetValue(map.uniqueID, out MapState? state) && state.markKind.Count > 0)
        {
            state.markKind.Clear();
            state.markTick.Clear();
            Version++;
        }
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % TidyEvery == 0)
        {
            Tidy();
        }
    }

    // Drops marks past their time and the records of maps that are gone.
    private void Tidy()
    {
        int now = Find.TickManager.TicksGame;
        int keep = Settings.newHours * GenDate.TicksPerHour;
        List<int>? gone = null;
        foreach (KeyValuePair<int, MapState> entry in maps)
        {
            if (Find.Maps.Find(map => map.uniqueID == entry.Key) == null)
            {
                (gone ??= new List<int>()).Add(entry.Key);
                continue;
            }

            List<ThingDef>? expired = null;
            foreach (KeyValuePair<ThingDef, int> mark in entry.Value.markTick)
            {
                if (now - mark.Value >= keep)
                {
                    (expired ??= new List<ThingDef>()).Add(mark.Key);
                }
            }

            if (expired != null)
            {
                foreach (ThingDef def in expired)
                {
                    entry.Value.markTick.Remove(def);
                    entry.Value.markKind.Remove(def);
                }

                Version++;
            }
        }

        if (gone != null)
        {
            foreach (int id in gone)
            {
                maps.Remove(id);
            }

            Version++;
        }
    }

    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedSeen = new List<string>();
            foreach (ThingDef def in seen)
            {
                savedSeen.Add(def.defName);
            }

            savedStocked = new List<string>();
            savedOut = new Dictionary<string, int>();
            savedMarkTick = new Dictionary<string, int>();
            savedMarkKind = new Dictionary<string, int>();
            foreach (KeyValuePair<int, MapState> entry in maps)
            {
                string prefix = entry.Key + "/";
                foreach (ThingDef def in entry.Value.stocked)
                {
                    savedStocked.Add(prefix + def.defName);
                }

                foreach (KeyValuePair<ThingDef, int> pair in entry.Value.outSince)
                {
                    savedOut[prefix + pair.Key.defName] = pair.Value;
                }

                foreach (KeyValuePair<ThingDef, int> pair in entry.Value.markTick)
                {
                    savedMarkTick[prefix + pair.Key.defName] = pair.Value;
                    savedMarkKind[prefix + pair.Key.defName] = entry.Value.markKind[pair.Key];
                }

                // A map with nothing stored still needs an entry, or its stock would be taken as seen again after loading.
                savedStocked.Add(prefix);
            }
        }

        Scribe_Collections.Look(ref savedSeen, "seen", LookMode.Value);
        Scribe_Collections.Look(ref savedStocked, "stocked", LookMode.Value);
        Scribe_Collections.Look(ref savedOut, "outSince", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref savedMarkTick, "markTick", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref savedMarkKind, "markKind", LookMode.Value, LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Load();
        }

        if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.LoadingVars)
        {
            savedSeen = null;
            savedStocked = null;
            savedOut = null;
            savedMarkTick = null;
            savedMarkKind = null;
        }
    }

    private void Load()
    {
        seen.Clear();
        maps.Clear();
        if (savedSeen != null)
        {
            foreach (string defName in savedSeen)
            {
                if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) is ThingDef def)
                {
                    seen.Add(def);
                }
            }
        }

        if (savedStocked != null)
        {
            foreach (string id in savedStocked)
            {
                if (Parse(id, out MapState? state, out ThingDef? def) && def != null)
                {
                    state.stocked.Add(def);
                }
            }
        }

        if (savedOut != null)
        {
            foreach (KeyValuePair<string, int> entry in savedOut)
            {
                if (Parse(entry.Key, out MapState? state, out ThingDef? def) && def != null)
                {
                    state.outSince[def] = entry.Value;
                }
            }
        }

        if (savedMarkTick != null && savedMarkKind != null)
        {
            foreach (KeyValuePair<string, int> entry in savedMarkTick)
            {
                if (savedMarkKind.TryGetValue(entry.Key, out int kind) && Parse(entry.Key, out MapState? state, out ThingDef? def) && def != null)
                {
                    state.markTick[def] = entry.Value;
                    state.markKind[def] = kind;
                }
            }
        }

        Version++;
    }

    // "mapId/defName", or "mapId/" for a map with nothing stored.
    private bool Parse(string id, out MapState state, out ThingDef? def)
    {
        state = null!;
        def = null;
        int slash = id.IndexOf('/');
        if (slash <= 0 || !int.TryParse(id.Substring(0, slash), out int mapId))
        {
            return false;
        }

        if (!maps.TryGetValue(mapId, out MapState? found))
        {
            found = new MapState();
            maps[mapId] = found;
        }

        state = found;
        if (slash + 1 < id.Length)
        {
            def = DefDatabase<ThingDef>.GetNamedSilentFail(id.Substring(slash + 1));
            return def != null;
        }

        return true;
    }
}
