using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Renders a row of condition icons (DoTs, buffs, debuffs) below a CombatHealthBar.
///
/// Setup:
///   1. Add a horizontal LayoutGroup child under your health bar prefab root.
///   2. Assign that child to the `iconContainer` field on this component.
///   3. Create a small prefab: Image on root (background/sprite), TextMeshProUGUI child
///      (emoji fallback), optional second TextMeshProUGUI child for duration countdown.
///   4. This component lives on the same GameObject as CombatHealthBar.
///
/// Sprite priority: custom definition sprite → built-in sprite → emoji fallback.
/// CombatHealthBar calls RefreshConditions() after any change to activeEffects.
/// </summary>
public class ConditionIconRow : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════════
    // CUSTOM CONDITION DEFINITION
    // Add entries here in the Inspector to register any condition that doesn't
    // have a built-in bool flag (isBleed, isStun, etc.).
    // Matching is done against effect.effectName and effect.iconKey (case-insensitive).
    // ══════════════════════════════════════════════════════════════════════════

    [System.Serializable]
    public class ConditionDefinition
    {
        [Tooltip("Must match effect.effectName or effect.iconKey exactly (case-insensitive).")]
        public string conditionName;

        [Tooltip("Sprite to display. If null, falls back to emoji.")]
        public Sprite sprite;

        [Tooltip("Background tint color for the icon slot.")]
        public Color color = new Color(0.4f, 0.1f, 0.1f, 0.85f);

        [Tooltip("True = debuff (red tint fallback), False = buff (green tint fallback).")]
        public bool isNegative = true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BUILT-IN SPRITES
    // Assign your own sprites for the standard condition types here.
    // Any slot left null falls back to the emoji defined in ResolveEmoji().
    // ══════════════════════════════════════════════════════════════════════════

    [System.Serializable]
    public class BuiltInSprites
    {
        public Sprite dot;
        public Sprite bleed;
        public Sprite stun;
        public Sprite silence;
        public Sprite barrier;
        public Sprite mark;
        public Sprite taunt;
        public Sprite curse;
        public Sprite exposed;
        public Sprite enrage;
        public Sprite haste;
        public Sprite riposte;
        public Sprite primed;
        public Sprite defense;
        public Sprite statBoost;
        public Sprite lifesteal;
        public Sprite buff;
        public Sprite debuff;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ══════════════════════════════════════════════════════════════════════════

    [Header("References")]
    [Tooltip("Horizontal layout group that holds the icon slots.")]
    public Transform iconContainer;

    [Tooltip("Prefab: Image on root (background/sprite) + TextMeshProUGUI child (emoji fallback) " +
             "+ optional second TextMeshProUGUI child (duration countdown).")]
    public GameObject iconSlotPrefab;

    [Header("Built-In Condition Sprites")]
    [Tooltip("Assign sprites for the standard condition types. Leave any slot null to use emoji fallback.")]
    public BuiltInSprites builtInSprites = new BuiltInSprites();

    [Header("Custom Conditions")]
    [Tooltip("Register any conditions that don't have a built-in bool flag. " +
             "Matched against effect.effectName and effect.iconKey (case-insensitive).")]
    public List<ConditionDefinition> customConditions = new List<ConditionDefinition>();

    [Header("Layout")]
    [Tooltip("Maximum icons shown before a '+N more' slot is displayed.")]
    public int maxVisibleIcons = 6;

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ══════════════════════════════════════════════════════════════════════════

    private readonly List<GameObject> _slots = new List<GameObject>();

    // Cached lookup built once from customConditions list (avoids per-frame linear search).
    private Dictionary<string, ConditionDefinition> _customLookup;

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this whenever the entity's activeEffects list changes.
    /// Rebuilds the icon row from scratch (lists are tiny, no perf concern).
    /// </summary>
    public void RefreshConditions(List<StatusEffect> activeEffects)
    {
        if (iconContainer == null || iconSlotPrefab == null) return;

        BuildCustomLookupIfNeeded();

        // Collapse duplicates: "Bleed x3" instead of three separate icons.
        var collapsed = CollapseEffects(activeEffects);

        EnsureSlotCount(collapsed.Count + 1); // +1 for potential overflow slot

        int shown = 0;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (i >= collapsed.Count || shown >= maxVisibleIcons)
            {
                int overflow = collapsed.Count - maxVisibleIcons;
                if (i == maxVisibleIcons && overflow > 0)
                {
                    // Overflow slot: shows "+N" with no sprite
                    SetSlotEmoji(_slots[i], $"+{overflow}", new Color(0.53f, 0.53f, 0.53f, 0.85f), true);
                    shown++;
                }
                else
                {
                    _slots[i].SetActive(false);
                }
                continue;
            }

            (StatusEffect effect, int count) = collapsed[i];

            Sprite sprite = ResolveSprite(effect);
            Color color = ResolveColor(effect);
            int dur = effect.duration;

            if (sprite != null)
            {
                SetSlotSprite(_slots[i], sprite, color, count, dur);
            }
            else
            {
                string emoji = ResolveEmoji(effect);
                string label = count > 1 ? $"{emoji}\n×{count}" : emoji;
                SetSlotEmoji(_slots[i], label, color, true, dur);
            }

            shown++;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Slot configuration
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Configures a slot to display a sprite. Hides the emoji TMP child.
    /// </summary>
    private void SetSlotSprite(GameObject slot, Sprite sprite, Color bgColor, int stackCount, int duration)
    {
        slot.SetActive(true);

        Image bg = slot.GetComponent<Image>();
        if (bg != null)
        {
            bg.sprite = sprite;
            bg.color = bgColor;
            // Use Simple mode so the sprite fills the slot cleanly.
            bg.type = Image.Type.Simple;
            bg.preserveAspect = true;
        }

        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>(true);

        // First TMP child: show stack count if > 1, hide otherwise.
        if (texts.Length > 0)
        {
            if (stackCount > 1)
            {
                texts[0].text = $"×{stackCount}";
                texts[0].gameObject.SetActive(true);
            }
            else
            {
                texts[0].gameObject.SetActive(false);
            }
        }

        // Second TMP child: duration countdown.
        if (texts.Length > 1)
        {
            if (duration > 0)
            {
                texts[1].text = duration.ToString();
                texts[1].gameObject.SetActive(true);
            }
            else
            {
                texts[1].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Configures a slot to display an emoji/text label. Clears any sprite.
    /// </summary>
    private void SetSlotEmoji(GameObject slot, string label, Color bgColor, bool active, int duration = -1)
    {
        slot.SetActive(active);
        if (!active) return;

        Image bg = slot.GetComponent<Image>();
        if (bg != null)
        {
            bg.sprite = null;
            bg.color = bgColor;
            bg.type = Image.Type.Simple;
        }

        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (texts.Length > 0)
        {
            texts[0].text = label;
            texts[0].gameObject.SetActive(true);
        }

        if (texts.Length > 1)
        {
            if (duration > 0)
            {
                texts[1].text = duration.ToString();
                texts[1].gameObject.SetActive(true);
            }
            else
            {
                texts[1].gameObject.SetActive(false);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Slot pooling
    // ══════════════════════════════════════════════════════════════════════════

    private void EnsureSlotCount(int needed)
    {
        while (_slots.Count < needed)
        {
            GameObject slot = Instantiate(iconSlotPrefab, iconContainer);
            slot.SetActive(false);
            _slots.Add(slot);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Effect collapsing
    // ══════════════════════════════════════════════════════════════════════════

    private List<(StatusEffect effect, int count)> CollapseEffects(List<StatusEffect> effects)
    {
        var result = new List<(StatusEffect, int)>();
        var seen = new Dictionary<string, int>(); // effectName → result index

        foreach (StatusEffect e in effects)
        {
            string key = e.effectName;
            if (seen.TryGetValue(key, out int idx))
            {
                var (existing, count) = result[idx];
                result[idx] = (existing, count + 1);
            }
            else
            {
                seen[key] = result.Count;
                result.Add((e, 1));
            }
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Custom lookup cache
    // ══════════════════════════════════════════════════════════════════════════

    private void BuildCustomLookupIfNeeded()
    {
        if (_customLookup != null) return;

        _customLookup = new Dictionary<string, ConditionDefinition>(System.StringComparer.OrdinalIgnoreCase);
        foreach (ConditionDefinition def in customConditions)
        {
            if (!string.IsNullOrEmpty(def.conditionName))
                _customLookup[def.conditionName] = def;
        }
    }

    /// <summary>
    /// Call this if you add or remove entries from customConditions at runtime.
    /// Normally not needed — the list is set up in the Inspector before play.
    /// </summary>
    public void InvalidateCustomLookup()
    {
        _customLookup = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE — Resolution: sprite, color, emoji
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sprite resolution order:
    ///   1. Custom definition (matched by effectName or iconKey)
    ///   2. Built-in sprite for the auto-detected key
    ///   3. null → caller falls back to emoji
    /// </summary>
    private Sprite ResolveSprite(StatusEffect effect)
    {
        // 1. Custom definition match — check effectName first, then iconKey.
        ConditionDefinition def = FindCustomDefinition(effect);
        if (def != null && def.sprite != null)
            return def.sprite;

        // 2. Built-in sprite.
        string key = string.IsNullOrEmpty(effect.iconKey)
            ? AutoDetectKey(effect)
            : effect.iconKey.ToLower();

        return GetBuiltInSprite(key);
    }

    /// <summary>
    /// Color resolution order:
    ///   1. effect.colorHex (set on the StatusEffect asset)
    ///   2. Custom definition color
    ///   3. Default color for the auto-detected key
    /// </summary>
    private Color ResolveColor(StatusEffect effect)
    {
        // 1. Per-effect colorHex override.
        if (!string.IsNullOrEmpty(effect.colorHex) &&
            ColorUtility.TryParseHtmlString(effect.colorHex, out Color fromHex))
            return new Color(fromHex.r, fromHex.g, fromHex.b, 0.85f);

        // 2. Custom definition color.
        ConditionDefinition def = FindCustomDefinition(effect);
        if (def != null)
            return new Color(def.color.r, def.color.g, def.color.b, 0.85f);

        // 3. Default per-key color.
        string key = string.IsNullOrEmpty(effect.iconKey)
            ? AutoDetectKey(effect)
            : effect.iconKey.ToLower();

        return GetBuiltInColor(key, effect.isNegativeEffect);
    }

    /// <summary>
    /// Emoji used when no sprite is available.
    /// </summary>
    private string ResolveEmoji(StatusEffect effect)
    {
        // Custom definition has no emoji field — fall through to key-based table.
        string key = string.IsNullOrEmpty(effect.iconKey)
            ? AutoDetectKey(effect)
            : effect.iconKey.ToLower();

        switch (key)
        {
            case "dot": return "🔥";
            case "bleed": return "🩸";
            case "stun": return "💫";
            case "silence": return "🔇";
            case "barrier": return "🛡";
            case "mark": return "🎯";
            case "taunt": return "😤";
            case "curse": return "🖤";
            case "exposed": return "💢";
            case "enrage": return "😡";
            case "haste": return "⚡";
            case "riposte": return "⚔️";
            case "primed": return "💥";
            case "defense": return "🔰";
            case "statboost": return "⬆️";
            case "lifesteal": return "💉";
            case "buff": return "✨";
            default:
                return string.IsNullOrEmpty(effect.effectName)
                    ? "?"
                    : effect.effectName.Substring(0, 1).ToUpper();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ConditionDefinition FindCustomDefinition(StatusEffect effect)
    {
        if (_customLookup == null) return null;

        if (!string.IsNullOrEmpty(effect.effectName) &&
            _customLookup.TryGetValue(effect.effectName, out ConditionDefinition byName))
            return byName;

        if (!string.IsNullOrEmpty(effect.iconKey) &&
            _customLookup.TryGetValue(effect.iconKey, out ConditionDefinition byKey))
            return byKey;

        return null;
    }

    private Sprite GetBuiltInSprite(string key)
    {
        switch (key)
        {
            case "dot": return builtInSprites.dot;
            case "bleed": return builtInSprites.bleed;
            case "stun": return builtInSprites.stun;
            case "silence": return builtInSprites.silence;
            case "barrier": return builtInSprites.barrier;
            case "mark": return builtInSprites.mark;
            case "taunt": return builtInSprites.taunt;
            case "curse": return builtInSprites.curse;
            case "exposed": return builtInSprites.exposed;
            case "enrage": return builtInSprites.enrage;
            case "haste": return builtInSprites.haste;
            case "riposte": return builtInSprites.riposte;
            case "primed": return builtInSprites.primed;
            case "defense": return builtInSprites.defense;
            case "statboost": return builtInSprites.statBoost;
            case "lifesteal": return builtInSprites.lifesteal;
            case "buff": return builtInSprites.buff;
            case "debuff": return builtInSprites.debuff;
            default: return null;
        }
    }

    private Color GetBuiltInColor(string key, bool isNegative)
    {
        string hex;
        switch (key)
        {
            case "dot": hex = "#CC4400"; break;
            case "bleed": hex = "#AA0022"; break;
            case "stun": hex = "#CCAA00"; break;
            case "silence": hex = "#664488"; break;
            case "barrier": hex = "#2266CC"; break;
            case "mark": hex = "#FF6600"; break;
            case "taunt": hex = "#884400"; break;
            case "curse": hex = "#220033"; break;
            case "exposed": hex = "#CC2200"; break;
            case "enrage": hex = "#880000"; break;
            case "haste": hex = "#CCBB00"; break;
            case "riposte": hex = "#446688"; break;
            case "primed": hex = "#FF8800"; break;
            case "defense": hex = "#226688"; break;
            case "statboost": hex = "#228844"; break;
            case "lifesteal": hex = "#AA2244"; break;
            case "buff": hex = "#336633"; break;
            default: hex = isNegative ? "#662222" : "#224422"; break;
        }

        ColorUtility.TryParseHtmlString(hex, out Color c);
        return new Color(c.r, c.g, c.b, 0.85f);
    }

    /// <summary>
    /// Inspects bool flags to determine a key when iconKey is not set.
    /// Priority order matches combat resolution (most specific first).
    /// </summary>
    private string AutoDetectKey(StatusEffect effect)
    {
        if (effect.isStun) return "stun";
        if (effect.isSilence) return "silence";
        if (effect.isBleed) return "bleed";
        if (effect.damageOverTime > 0) return "dot";
        if (effect.isBarrier) return "barrier";
        if (effect.isMark) return "mark";
        if (effect.isTaunt) return "taunt";
        if (effect.isCurse) return "curse";
        if (effect.isExposed) return "exposed";
        if (effect.isEnrage) return "enrage";
        if (effect.isHaste) return "haste";
        if (effect.isRiposte) return "riposte";
        if (effect.isPrimed) return "primed";
        if (effect.temporaryDefenseBonus > 0 || effect.baseDefenseAmount > 0) return "defense";
        if (effect.statBoostAmount > 0) return "statboost";
        if (effect.lifestealPercent > 0) return "lifesteal";
        return effect.isNegativeEffect ? "debuff" : "buff";
    }
}
