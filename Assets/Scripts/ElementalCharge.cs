using UnityEngine;
using System;

/// <summary>
/// Represents the types of elemental charges a mage can accumulate
/// </summary>
[Serializable]
public enum ElementType
{
    None,
    Fire,
    Frost,
    Arcane,
    Lightning,
    Acid,
    Shadow,
    Holy
}

/// <summary>
/// Represents a single elemental charge stored by a mage
/// </summary>
[Serializable]
public class ElementalCharge
{
    public ElementType element;
    public float timestamp; // When this charge was gained (for visual effects)

    public ElementalCharge(ElementType type)
    {
        element = type;
        timestamp = Time.time;
    }
}

/// <summary>
/// Helper class for comparing charge combinations
/// </summary>
public static class ChargeComboHelper
{
    /// <summary>
    /// Convert a list of 4 charges into a sorted combo signature for lookup
    /// Example: [Fire, Fire, Arcane, Frost] -> "Arcane+Fire+Fire+Frost"
    /// </summary>
    public static string GetComboSignature(ElementalCharge[] charges)
    {
        if (charges == null || charges.Length != 4)
            return "";

        // Extract element types
        string[] elements = new string[4];
        for (int i = 0; i < 4; i++)
        {
            elements[i] = charges[i].element.ToString();
        }

        // Sort alphabetically for consistent lookup
        Array.Sort(elements);

        // Join with "+" separator
        return string.Join("+", elements);
    }

    /// <summary>
    /// Get a display-friendly version of the combo
    /// Example: "2x Fire, 1x Arcane, 1x Frost"
    /// </summary>
    public static string GetComboDisplayName(ElementalCharge[] charges)
    {
        if (charges == null || charges.Length != 4)
            return "";

        // Count occurrences of each element
        var counts = new System.Collections.Generic.Dictionary<ElementType, int>();
        foreach (var charge in charges)
        {
            if (!counts.ContainsKey(charge.element))
                counts[charge.element] = 0;
            counts[charge.element]++;
        }

        // Build display string
        var parts = new System.Collections.Generic.List<string>();
        foreach (var kvp in counts)
        {
            if (kvp.Value > 1)
                parts.Add($"{kvp.Value}x {kvp.Key}");
            else
                parts.Add(kvp.Key.ToString());
        }

        return string.Join(", ", parts);
    }
}
