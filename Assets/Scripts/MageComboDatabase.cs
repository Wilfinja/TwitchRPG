using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton database that holds all possible mage charge combos
/// Attach to a GameObject in the scene
/// </summary>
public class MageComboDatabase : MonoBehaviour
{
    public static MageComboDatabase Instance;

    [Header("All Mage Combos")]
    [Tooltip("Assign all MageComboData ScriptableObjects here")]
    public List<MageComboData> allCombos = new List<MageComboData>();

    // Internal lookup dictionary for fast combo resolution
    private Dictionary<string, MageComboData> comboLookup = new Dictionary<string, MageComboData>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        BuildComboLookup();
    }

    /// <summary>
    /// Build the internal lookup dictionary from the combo list
    /// </summary>
    void BuildComboLookup()
    {
        comboLookup.Clear();

        foreach (var combo in allCombos)
        {
            if (combo == null) continue;

            string signature = combo.GetComboSignature();
            if (string.IsNullOrEmpty(signature))
            {
                Debug.LogWarning($"[MageComboDatabase] Combo '{combo.comboName}' has invalid charge configuration!");
                continue;
            }

            if (!comboLookup.ContainsKey(signature))
            {
                comboLookup.Add(signature, combo);
                Debug.Log($"[MageComboDatabase] Registered combo: {combo.comboName} ({signature})");
            }
            else
            {
                Debug.LogWarning($"[MageComboDatabase] Duplicate combo signature: {signature} for '{combo.comboName}'");
            }
        }

        Debug.Log($"[MageComboDatabase] Loaded {comboLookup.Count} unique combos");
    }

    /// <summary>
    /// Look up a combo based on the current charges
    /// Returns null if no combo exists for this combination
    /// </summary>
    public MageComboData GetCombo(ElementalCharge[] charges)
    {
        if (charges == null || charges.Length != 4)
        {
            Debug.LogWarning("[MageComboDatabase] Invalid charge array for combo lookup");
            return null;
        }

        string signature = ChargeComboHelper.GetComboSignature(charges);

        if (comboLookup.TryGetValue(signature, out MageComboData combo))
        {
            return combo;
        }

        // No combo found for this combination
        return null;
    }

    /// <summary>
    /// Check if a specific combination has a registered combo
    /// </summary>
    public bool HasCombo(ElementalCharge[] charges)
    {
        return GetCombo(charges) != null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only helper to print all registered combos
    /// </summary>
    [ContextMenu("Print All Combos")]
    void PrintAllCombos()
    {
        Debug.Log("===== REGISTERED MAGE COMBOS =====");
        foreach (var kvp in comboLookup)
        {
            Debug.Log($"{kvp.Value.comboName}: {kvp.Key}");
        }
    }
#endif
}