using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils;

/// <summary>
/// Shared, process-lifetime store of element pairs unjoined by
/// UnjoinWallJoins / UnjoinColumnJoins / UnjoinElementJoins, so
/// RejoinWallJoins can restore them later in the same Revit session.
/// Mirrors REVIT_MCP_study's CommandExecutor._unjoinedPairs, which lived on
/// the shared command-dispatcher instance; this codebase splits each tool
/// into its own Command/EventHandler class, so the pair list needs a home
/// that outlives any single handler instance.
/// </summary>
public static class WallJoinTracker
{
    private static readonly List<Tuple<ElementId, ElementId>> _unjoinedPairs = new();

    public static IReadOnlyList<Tuple<ElementId, ElementId>> Pairs => _unjoinedPairs;

    public static int Count => _unjoinedPairs.Count;

    public static void Clear() => _unjoinedPairs.Clear();

    public static void Add(ElementId a, ElementId b) => _unjoinedPairs.Add(new Tuple<ElementId, ElementId>(a, b));

    public static string PairKey(ElementId a, ElementId b)
    {
        int av = a.GetIntValue();
        int bv = b.GetIntValue();
        return av < bv ? $"{av}-{bv}" : $"{bv}-{av}";
    }

    public static HashSet<string> ExistingKeys()
    {
        var set = new HashSet<string>();
        foreach (var pair in _unjoinedPairs) set.Add(PairKey(pair.Item1, pair.Item2));
        return set;
    }

    /// <summary>
    /// Attempts to unjoin <paramref name="source"/> from each element in
    /// <paramref name="neighbors"/> that isn't already recorded, recording
    /// each successful unjoin. Returns the count actually unjoined.
    /// </summary>
    public static int TryUnjoinBatch(Document doc, Element source, IEnumerable<Element> neighbors, HashSet<string> existingKeys)
    {
        int count = 0;
        foreach (Element neighbor in neighbors)
        {
            if (neighbor.Id == source.Id) continue;
            string key = PairKey(source.Id, neighbor.Id);
            if (existingKeys.Contains(key)) continue;

            try
            {
                if (JoinGeometryUtils.AreElementsJoined(doc, source, neighbor))
                {
                    JoinGeometryUtils.UnjoinGeometry(doc, source, neighbor);
                    Add(source.Id, neighbor.Id);
                    existingKeys.Add(key);
                    count++;
                }
            }
            catch
            {
                // Ignore pairs that cannot be unjoined (e.g. not actually joined, or API refuses).
            }
        }
        return count;
    }
}
