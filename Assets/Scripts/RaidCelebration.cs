using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the "we're being raided!" moment: a brass fanfare, a banner
/// notification, confetti, and every on-screen character hopping in a ripple.
///
/// Attach to any GameObject in the scene, then drag it into the
/// "Raid Celebration" slot on TwitchOverlayManager. Everything is optional:
/// with nothing assigned you still get a synthesized fanfare and the hops.
/// </summary>
public class RaidCelebration : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Your own trumpet stinger. Leave empty to use the built-in synthesized fanfare.")]
    [SerializeField] private AudioClip fanfareClip;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.7f;

    [Header("Character Cheer")]
    [SerializeField] private float hopHeight = 0.6f;
    [SerializeField] private int hopsPerCheer = 3;
    [SerializeField] private float cheerDuration = 2.4f;
    [Tooltip("Max random delay per character so the crowd ripples instead of moving in lockstep.")]
    [SerializeField] private float maxStaggerSeconds = 0.6f;

    [Header("Raid Size Scaling")]
    [Tooltip("Raids at or above this many viewers get the big treatment (extra confetti + taller hops).")]
    [SerializeField] private int bigRaidThreshold = 25;
    [SerializeField] private int confettiBurstsSmall = 1;
    [SerializeField] private int confettiBurstsBig = 4;
    [SerializeField] private float bigRaidHopMultiplier = 1.5f;

    [Header("Coin Rain (optional)")]
    [Tooltip("Coins that rain down for the raiders to grab. Set to 0 to disable.")]
    [SerializeField] private int coinsPerRaider = 0;
    [SerializeField] private int maxCoins = 60;

    [Header("Behavior")]
    [Tooltip("Ignore new raids for this long after one starts, so back-to-back raids can't stack the audio.")]
    [SerializeField] private float cooldownSeconds = 6f;

    private AudioSource audioSource;
    private AudioClip generatedFanfare;
    private float nextAllowedTime = 0f;

    private static RaidCelebration _instance;
    public static RaidCelebration Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<RaidCelebration>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // plain 2D, not positional
    }

    /// <summary>Call on the Unity main thread (TwitchOverlayManager dispatches for you).</summary>
    public void Play(string raiderName, int viewerCount)
    {
        if (Time.time < nextAllowedTime)
        {
            Debug.Log($"[RaidCelebration] Raid from {raiderName} arrived during cooldown, skipping effects.");
            return;
        }
        nextAllowedTime = Time.time + cooldownSeconds;

        bool isBig = viewerCount >= bigRaidThreshold;
        Debug.Log($"[RaidCelebration] {raiderName} raiding with {viewerCount} viewers (big: {isBig})");

        PlayFanfare();

        string raiders = viewerCount == 1 ? "raider" : "raiders";
        OnScreenNotification.Instance?.ShowSuccess(
            $"⚔️ RAID! {raiderName} brings {viewerCount} {raiders}!\nWelcome, adventurers!");

        StartCoroutine(ConfettiRoutine(isBig ? confettiBurstsBig : confettiBurstsSmall));
        CheerAllCharacters(isBig);

        if (coinsPerRaider > 0 && CoinSpawner.Instance != null)
        {
            CoinSpawner.Instance.SpawnCoins(Mathf.Clamp(viewerCount * coinsPerRaider, 1, maxCoins));
        }
    }

    // ==================== VISUALS ====================

    private IEnumerator ConfettiRoutine(int bursts)
    {
        for (int i = 0; i < bursts; i++)
        {
            ParticleEffectManager.Instance?.TriggerConfetti();
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void CheerAllCharacters(bool isBig)
    {
        if (CharacterSpawner.Instance == null) return;

        List<OnScreenCharacter> characters = CharacterSpawner.Instance.GetAllCharacters();
        float height = hopHeight * (isBig ? bigRaidHopMultiplier : 1f);

        foreach (OnScreenCharacter character in characters)
        {
            if (character == null) continue;
            character.PlayRaidCheer(Random.Range(0f, maxStaggerSeconds), cheerDuration, height, hopsPerCheer);
        }
    }

    // ==================== AUDIO ====================

    private void PlayFanfare()
    {
        AudioClip clip = fanfareClip != null ? fanfareClip : GetGeneratedFanfare();
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GetGeneratedFanfare()
    {
        if (generatedFanfare == null) generatedFanfare = BuildFanfare();
        return generatedFanfare;
    }

    // A classic bugle-call shape: short-short-long "da da DAAAA", twice, ending high.
    // (frequency Hz, seconds). Frequency 0 = rest.
    private static readonly (float freq, float dur)[] Melody =
    {
        (392.00f, 0.16f), // G4
        (0f,      0.03f),
        (392.00f, 0.16f), // G4
        (0f,      0.03f),
        (523.25f, 0.42f), // C5
        (0f,      0.06f),
        (392.00f, 0.16f), // G4
        (0f,      0.03f),
        (523.25f, 0.16f), // C5
        (0f,      0.03f),
        (659.25f, 0.42f), // E5
        (0f,      0.06f),
        (523.25f, 0.18f), // C5
        (659.25f, 0.18f), // E5
        (783.99f, 0.85f), // G5 (held finish)
    };

    // Builds a brassy tone in code: a few harmonics, a little vibrato on long notes,
    // and a trumpet-ish attack/decay envelope. No audio files required.
    private AudioClip BuildFanfare()
    {
        const int sampleRate = 44100;

        float totalSeconds = 0f;
        foreach (var n in Melody) totalSeconds += n.dur;
        totalSeconds += 0.25f; // tail

        int totalSamples = Mathf.CeilToInt(totalSeconds * sampleRate);
        float[] data = new float[totalSamples];

        // Harmonic amplitudes. Trumpets are strong in the upper partials.
        float[] harmonics = { 1.0f, 0.75f, 0.55f, 0.4f, 0.28f, 0.18f };

        int cursor = 0;
        foreach (var note in Melody)
        {
            int noteSamples = Mathf.CeilToInt(note.dur * sampleRate);

            if (note.freq > 0f)
            {
                for (int i = 0; i < noteSamples && cursor + i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float progress = (float)i / noteSamples;

                    // Envelope: fast attack, short dip, sustain, quick release.
                    float attack = Mathf.Clamp01(t / 0.03f);
                    float release = Mathf.Clamp01((note.dur - t) / 0.06f);
                    float body = 0.85f + 0.15f * Mathf.Exp(-t * 8f);
                    float env = attack * release * body;

                    // Gentle vibrato that only kicks in on the longer notes.
                    float vibrato = note.dur > 0.3f ? Mathf.Sin(2f * Mathf.PI * 5.5f * t) * 0.004f * progress : 0f;

                    float sample = 0f;
                    for (int h = 0; h < harmonics.Length; h++)
                    {
                        float f = note.freq * (h + 1) * (1f + vibrato);
                        sample += harmonics[h] * Mathf.Sin(2f * Mathf.PI * f * t);
                    }

                    data[cursor + i] += sample * env * 0.16f;
                }
            }

            cursor += noteSamples;
        }

        // Soft clip so stacked harmonics can't distort.
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)System.Math.Tanh(data[i]);
        }

        AudioClip clip = AudioClip.Create("RaidFanfare_Generated", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
