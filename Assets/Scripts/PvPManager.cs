using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages 1v1 PvP challenges, betting, spectators, and combat flow
/// </summary>
public class PvPManager : MonoBehaviour
{
    public static PvPManager Instance;

    [Header("PvP State")]
    public bool pvpActive = false;
    public PvPMatch currentMatch;

    [Header("Challenge Settings")]
    [SerializeField] private float challengeTimeoutSeconds = 60f;
    [SerializeField] private float challengeCooldownMinutes = 20f;
    [SerializeField] private int minWager = 25;

    [Header("XP Rewards")]
    [SerializeField] private int winnerXP = 50;
    [SerializeField] private int loserXP = 25;

    [Header("Betting Settings")]
    [SerializeField] private int voteCost = 25;

    [Header("Combat Positions")]
    [SerializeField] private Vector3 fighter1Position = new Vector3(-2f, 0f, 0f);
    [SerializeField] private Vector3 fighter2Position = new Vector3(2f, 0f, 0f);

    [Header("Spectator Positions")]
    [SerializeField] private Vector3[] leftSpectatorPositions = new Vector3[5];
    [SerializeField] private Vector3[] rightSpectatorPositions = new Vector3[5];

    private Dictionary<string, DateTime> challengeCooldowns = new Dictionary<string, DateTime>();
    private Dictionary<string, PendingChallenge> pendingChallenges = new Dictionary<string, PendingChallenge>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Update()
    {
        // Check for expired challenges
        CheckExpiredChallenges();
    }

    #region Challenge System

    public string CreateChallenge(string challengerUserId, string challengerUsername, string targetUsername, int wager)
    {
        // Validation checks
        if (pvpActive)
            return $"{challengerUsername}: A PvP match is already in progress!";

        if (ExpeditionManager.Instance.currentExpedition.isActive)
            return $"{challengerUsername}: Cannot challenge during an expedition!";

        // Check cooldown
        if (IsOnCooldown(challengerUserId))
        {
            TimeSpan remaining = GetRemainingCooldown(challengerUserId);
            return $"{challengerUsername}: You're on PvP cooldown! {remaining.Minutes}m {remaining.Seconds}s remaining.";
        }

        // Get challenger data
        ViewerData challenger = RPGManager.Instance.GetViewer(challengerUserId);
        if (challenger == null || challenger.characterClass == CharacterClass.None)
            return $"{challengerUsername}: Choose a class first with !class";

        // Check if challenger is on screen
        OnScreenCharacter challengerChar = CharacterSpawner.Instance?.GetCharacter(challengerUserId);
        if (challengerChar == null)
            return $"{challengerUsername}: You must be on screen to challenge! Use !join";

        // Find target
        ViewerData target = FindViewerByUsername(targetUsername);
        if (target == null)
            return $"{challengerUsername}: Player '{targetUsername}' not found.";

        // Check if target is on screen
        OnScreenCharacter targetChar = CharacterSpawner.Instance?.GetCharacter(target.twitchUserId);
        if (targetChar == null)
            return $"{challengerUsername}: {target.username} must be on screen to accept challenges!";

        // Check if target can fight
        if (!target.CanTakeAction())
            return $"{challengerUsername}: {target.username} cannot accept challenges right now.";

        // Validate wager
        if (wager < minWager)
            return $"{challengerUsername}: Minimum wager is {minWager} coins!";

        if (wager > challenger.coins)
            return $"{challengerUsername}: You only have {challenger.coins} coins!";

        if (wager > target.coins)
            return $"{challengerUsername}: {target.username} only has {target.coins} coins!";

        // Check for existing challenge to this person
        if (pendingChallenges.ContainsKey(target.twitchUserId))
        {
            return $"{challengerUsername}: {target.username} already has a pending challenge!";
        }

        // Create pending challenge
        PendingChallenge challenge = new PendingChallenge
        {
            challengerUserId = challengerUserId,
            challengerUsername = challengerUsername,
            targetUserId = target.twitchUserId,
            targetUsername = target.username,
            wager = wager,
            expiresAt = DateTime.Now.AddSeconds(challengeTimeoutSeconds)
        };

        pendingChallenges[target.twitchUserId] = challenge;

        OnScreenNotification.Instance?.ShowNotification(
            $"⚔️ PVP CHALLENGE!\n" +
            $"{challengerUsername} challenges {target.username} to a duel!\n" +
            $"Wager: {wager} coins\n" +
            $"@{target.username} type !accept or !decline\n" +
            $"Expires in {challengeTimeoutSeconds} seconds"
        );

        return null; // Message already shown
    }

    public string AcceptChallenge(string targetUserId, string targetUsername)
    {
        if (!pendingChallenges.ContainsKey(targetUserId))
            return $"{targetUsername}: You don't have any pending challenges!";

        PendingChallenge challenge = pendingChallenges[targetUserId];

        // Verify both players are still valid
        ViewerData challenger = RPGManager.Instance.GetViewer(challenge.challengerUserId);
        ViewerData target = RPGManager.Instance.GetViewer(targetUserId);

        if (challenger == null || target == null)
        {
            pendingChallenges.Remove(targetUserId);
            return $"{targetUsername}: Challenge is no longer valid.";
        }

        // Re-verify funds
        if (challenge.wager > challenger.coins || challenge.wager > target.coins)
        {
            pendingChallenges.Remove(targetUserId);
            return $"{targetUsername}: One of the fighters can't afford the wager anymore!";
        }

        // Remove pending challenge
        pendingChallenges.Remove(targetUserId);

        // Add challenger to cooldown (20 min starts NOW)
        challengeCooldowns[challenge.challengerUserId] = DateTime.Now.AddMinutes(challengeCooldownMinutes);

        // Start the match!
        StartPvPMatch(challenge);

        return null; // Match start handles notifications
    }

    public string DeclineChallenge(string targetUserId, string targetUsername)
    {
        if (!pendingChallenges.ContainsKey(targetUserId))
            return $"{targetUsername}: You don't have any pending challenges!";

        PendingChallenge challenge = pendingChallenges[targetUserId];
        pendingChallenges.Remove(targetUserId);

        OnScreenNotification.Instance?.ShowNotification(
            $"{targetUsername} declined {challenge.challengerUsername}'s challenge!"
        );

        return null;
    }

    private void CheckExpiredChallenges()
    {
        List<string> expired = new List<string>();

        foreach (var kvp in pendingChallenges)
        {
            if (DateTime.Now >= kvp.Value.expiresAt)
            {
                expired.Add(kvp.Key);
                OnScreenNotification.Instance?.ShowNotification(
                    $"{kvp.Value.challengerUsername}'s challenge to {kvp.Value.targetUsername} expired."
                );
            }
        }

        foreach (string userId in expired)
        {
            pendingChallenges.Remove(userId);
        }
    }

    #endregion

    #region Match Flow

    private void StartPvPMatch(PendingChallenge challenge)
    {
        pvpActive = true;

        // Create match data
        currentMatch = new PvPMatch
        {
            fighter1UserId = challenge.challengerUserId,
            fighter1Username = challenge.challengerUsername,
            fighter2UserId = challenge.targetUserId,
            fighter2Username = challenge.targetUsername,
            wager = challenge.wager,
            bets = new Dictionary<string, PvPBet>()
        };

        OnScreenNotification.Instance?.ShowNotification(
            $"🔥 PVP MATCH BEGINS! 🔥\n" +
            $"{currentMatch.fighter1Username} vs {currentMatch.fighter2Username}\n" +
            $"Wager: {currentMatch.wager} coins each\n" +
            $"Type !bet @{currentMatch.fighter1Username} or !bet @{currentMatch.fighter2Username} to place bets! (25 coins)"
        );

        StartCoroutine(TransitionToPvP());
    }

    private IEnumerator TransitionToPvP()
    {
        // 1. Get all on-screen characters
        List<OnScreenCharacter> allCharacters = CharacterSpawner.Instance?.GetAllCharacters();
        if (allCharacters == null) yield break;

        // 2. Separate fighters from spectators
        OnScreenCharacter fighter1 = allCharacters.Find(c => c.GetUserId() == currentMatch.fighter1UserId);
        OnScreenCharacter fighter2 = allCharacters.Find(c => c.GetUserId() == currentMatch.fighter2UserId);

        List<OnScreenCharacter> spectators = allCharacters
            .Where(c => c.GetUserId() != currentMatch.fighter1UserId && c.GetUserId() != currentMatch.fighter2UserId)
            .ToList();

        // 3. Move fighters to center positions
        if (fighter1 != null)
            fighter1.EnterCombatMode(fighter1Position);

        if (fighter2 != null)
            fighter2.EnterCombatMode(fighter2Position);

        // 4. Move spectators to sides
        ArrangeSpectators(spectators);

        yield return new WaitForSeconds(2f); // Wait for movement

        // 5. Add CombatEntity components to fighters
        SetupFighter(fighter1, currentMatch.fighter1UserId, currentMatch.fighter1Username, 1);
        SetupFighter(fighter2, currentMatch.fighter2UserId, currentMatch.fighter2Username, 2);

        yield return new WaitForSeconds(1f);

        // 6. Start betting period (15 seconds)
        yield return StartCoroutine(BettingPeriod());

        // 7. Start combat
        CombatTurnManager.Instance?.StartCombat();
    }

    private void ArrangeSpectators(List<OnScreenCharacter> spectators)
    {
        int leftCount = 0;
        int rightCount = 0;

        foreach (var spectator in spectators)
        {
            // Alternate between left and right
            if (leftCount <= rightCount && leftCount < leftSpectatorPositions.Length)
            {
                spectator.EnterCombatMode(leftSpectatorPositions[leftCount]);
                leftCount++;
            }
            else if (rightCount < rightSpectatorPositions.Length)
            {
                spectator.EnterCombatMode(rightSpectatorPositions[rightCount]);
                rightCount++;
            }

            // TODO: Set cheering animation when available
            // spectator.SetAnimation("Cheer");
        }
    }

    private void SetupFighter(OnScreenCharacter character, string userId, string username, int position)
    {
        if (character == null) return;

        CombatEntity combatEntity = character.gameObject.GetComponent<CombatEntity>();
        if (combatEntity == null)
        {
            combatEntity = character.gameObject.AddComponent<CombatEntity>();
        }

        combatEntity.InitializePlayer(userId, username, position);

        // Full HP/Resources for PvP
        combatEntity.currentHealth = combatEntity.maxHealth;
        InitializeFullResources(combatEntity);

        CombatUIManager.Instance?.CreateHealthBar(combatEntity);
    }

    private void InitializeFullResources(CombatEntity entity)
    {
        switch (entity.characterClass)
        {
            case CharacterClass.Rogue:
                entity.sneakPoints = 0;
                break;
            case CharacterClass.Fighter:
                entity.currentStance = FighterStance.None;
                break;
            case CharacterClass.Mage:
                entity.mana = 100;
                break;
            case CharacterClass.Cleric:
                entity.wrath = 0;
                break;
            case CharacterClass.Ranger:
                entity.balance = 0;
                entity.comboCounter = 0;
                break;
        }
    }

    private IEnumerator BettingPeriod()
    {
        OnScreenNotification.Instance?.ShowNotification(
            "🎲 Betting is open for 15 seconds!\n" +
            $"!bet @{currentMatch.fighter1Username} or !bet @{currentMatch.fighter2Username}"
        );

        yield return new WaitForSeconds(15f);

        OnScreenNotification.Instance?.ShowNotification("Betting closed! Combat begins!");

        // Calculate pot
        int totalBets = currentMatch.bets.Values.Sum(b => b.amount);
        currentMatch.totalPot = (currentMatch.wager * 2) + totalBets;

        Debug.Log($"[PvP] Total pot: {currentMatch.totalPot} coins ({currentMatch.wager * 2} wager + {totalBets} bets)");
    }

    #endregion

    #region Betting System

    public string PlaceBet(string bettorUserId, string bettorUsername, string fighterUsername)
    {
        if (!pvpActive || currentMatch == null)
            return $"{bettorUsername}: No PvP match is active!";

        // Can't bet on yourself
        if (bettorUserId == currentMatch.fighter1UserId || bettorUserId == currentMatch.fighter2UserId)
            return $"{bettorUsername}: You can't bet on your own match!";

        // Check if already bet
        if (currentMatch.bets.ContainsKey(bettorUserId))
            return $"{bettorUsername}: You already placed a bet on {currentMatch.bets[bettorUserId].fighterUsername}!";

        // Validate fighter name
        string targetFighter = null;
        if (fighterUsername.ToLower() == currentMatch.fighter1Username.ToLower())
            targetFighter = currentMatch.fighter1Username;
        else if (fighterUsername.ToLower() == currentMatch.fighter2Username.ToLower())
            targetFighter = currentMatch.fighter2Username;
        else
            return $"{bettorUsername}: Must bet on {currentMatch.fighter1Username} or {currentMatch.fighter2Username}!";

        // Check funds
        ViewerData bettor = RPGManager.Instance.GetViewer(bettorUserId);
        if (bettor == null || bettor.coins < voteCost)
            return $"{bettorUsername}: You need {voteCost} coins to bet!";

        // Deduct coins
        bettor.coins -= voteCost;

        // Record bet
        currentMatch.bets[bettorUserId] = new PvPBet
        {
            bettorUserId = bettorUserId,
            bettorUsername = bettorUsername,
            fighterUsername = targetFighter,
            amount = voteCost
        };

        OnScreenNotification.Instance?.ShowSuccess($"{bettorUsername} bet {voteCost} coins on {targetFighter}!");

        return null;
    }

    #endregion

    #region Match End

    public void OnPvPMatchEnd(string winnerUserId)
    {
        if (!pvpActive || currentMatch == null) return;

        string winnerUsername = winnerUserId == currentMatch.fighter1UserId
            ? currentMatch.fighter1Username
            : currentMatch.fighter2Username;

        string loserUserId = winnerUserId == currentMatch.fighter1UserId
            ? currentMatch.fighter2UserId
            : currentMatch.fighter1UserId;

        string loserUsername = winnerUserId == currentMatch.fighter1UserId
            ? currentMatch.fighter2Username
            : currentMatch.fighter1Username;

        // Calculate rewards
        DistributeRewards(winnerUserId, winnerUsername, loserUserId, loserUsername);

        // Update records
        UpdatePvPRecords(winnerUserId, loserUserId);

        // Grant XP
        ExperienceManager.Instance?.AddExperience(winnerUserId, winnerXP);
        ExperienceManager.Instance?.AddExperience(loserUserId, loserXP);

        OnScreenNotification.Instance?.ShowNotification(
            $"🏆 {winnerUsername} WINS!\n" +
            $"Rewards distributed!\n" +
            $"Winner: +{winnerXP} XP\n" +
            $"Loser: +{loserXP} XP"
        );

        StartCoroutine(CleanupPvP());
    }

    private void DistributeRewards(string winnerUserId, string winnerUsername, string loserUserId, string loserUsername)
    {
        ViewerData winner = RPGManager.Instance.GetViewer(winnerUserId);
        ViewerData loser = RPGManager.Instance.GetViewer(loserUserId);

        if (winner == null || loser == null) return;

        // Take wager from loser
        loser.coins -= currentMatch.wager;

        // Winner gets their wager back + loser's wager
        int winnings = currentMatch.wager * 2;

        // Distribute betting winnings
        List<PvPBet> winningBets = currentMatch.bets.Values
            .Where(b => b.fighterUsername == winnerUsername)
            .ToList();

        int totalWinningBets = winningBets.Sum(b => b.amount);
        int totalBets = currentMatch.bets.Values.Sum(b => b.amount);

        if (totalWinningBets > 0)
        {
            // Distribute betting pot proportionally to winners
            foreach (var bet in winningBets)
            {
                ViewerData bettor = RPGManager.Instance.GetViewer(bet.bettorUserId);
                if (bettor != null)
                {
                    // Get original bet back + share of losing bets
                    float share = (float)bet.amount / totalWinningBets;
                    int payout = bet.amount + Mathf.RoundToInt((totalBets - totalWinningBets) * share);

                    bettor.coins += payout;

                    OnScreenNotification.Instance?.ShowSuccess(
                        $"{bet.bettorUsername} won {payout} coins from betting!"
                    );
                }
            }
        }

        winner.coins += winnings;

        Debug.Log($"[PvP] {winnerUsername} won {winnings} coins from wager");

        RPGManager.Instance.SaveGameData();
    }

    private void UpdatePvPRecords(string winnerUserId, string loserUserId)
    {
        ViewerData winner = RPGManager.Instance.GetViewer(winnerUserId);
        ViewerData loser = RPGManager.Instance.GetViewer(loserUserId);

        if (winner != null)
        {
            winner.pvpWins++;
        }

        if (loser != null)
        {
            winner.pvpLosses++;
        }

        RPGManager.Instance.SaveGameData();
    }

    private IEnumerator CleanupPvP()
    {
        yield return new WaitForSeconds(3f);

        // Remove CombatEntity components
        List<OnScreenCharacter> allCharacters = CharacterSpawner.Instance?.GetAllCharacters();
        if (allCharacters != null)
        {
            foreach (var character in allCharacters)
            {
                CombatEntity entity = character.GetComponent<CombatEntity>();
                if (entity != null)
                {
                    // Restore resources
                    ViewerData viewer = RPGManager.Instance.GetViewer(character.GetUserId());
                    if (viewer != null)
                    {
                        viewer.baseStats.currentHealth = entity.maxHealth;
                    }

                    Destroy(entity);
                }

                character.ExitCombatMode();
            }
        }

        yield return new WaitForSeconds(1f);

        currentMatch = null;
        pvpActive = false;
    }

    #endregion

    #region Helper Methods

    private bool IsOnCooldown(string userId)
    {
        if (!challengeCooldowns.ContainsKey(userId))
            return false;

        return DateTime.Now < challengeCooldowns[userId];
    }

    private TimeSpan GetRemainingCooldown(string userId)
    {
        if (!challengeCooldowns.ContainsKey(userId))
            return TimeSpan.Zero;

        return challengeCooldowns[userId] - DateTime.Now;
    }

    private ViewerData FindViewerByUsername(string username)
    {
        username = username.TrimStart('@').ToLower();
        var allViewers = RPGManager.Instance.GetAllViewers();

        foreach (var viewer in allViewers)
        {
            if (viewer.username.ToLower() == username)
            {
                return viewer;
            }
        }

        return null;
    }

    public string GetPvPStats(string username)
    {
        ViewerData viewer = FindViewerByUsername(username);
        if (viewer == null)
            return $"Player '{username}' not found.";

        int total = viewer.pvpWins + viewer.pvpLosses;
        float winRate = total > 0 ? ((float)viewer.pvpWins / total) * 100f : 0f;

        return $"═══ {viewer.username}'s PvP Stats ═══\n" +
               $"Wins: {viewer.pvpWins}\n" +
               $"Losses: {viewer.pvpLosses}\n" +
               $"Win Rate: {winRate:F1}%\n" +
               $"Total Matches: {total}";
    }

    public string GetPvPLeaderboard()
    {
        var topPlayers = RPGManager.Instance.GetAllViewers()
            .Where(v => v.pvpWins > 0)
            .OrderByDescending(v => v.pvpWins)
            .Take(10)
            .ToList();

        if (topPlayers.Count == 0)
            return "No PvP matches have been played yet!";

        string leaderboard = "═══ PVP LEADERBOARD (Top 10) ═══\n";

        for (int i = 0; i < topPlayers.Count; i++)
        {
            var player = topPlayers[i];
            int total = player.pvpWins + player.pvpLosses;
            float winRate = ((float)player.pvpWins / total) * 100f;

            leaderboard += $"{i + 1}. {player.username} - {player.pvpWins}W / {player.pvpLosses}L ({winRate:F0}%)\n";
        }

        return leaderboard;
    }

    #endregion
}

#region Data Structures

[Serializable]
public class PendingChallenge
{
    public string challengerUserId;
    public string challengerUsername;
    public string targetUserId;
    public string targetUsername;
    public int wager;
    public DateTime expiresAt;
}

[Serializable]
public class PvPMatch
{
    public string fighter1UserId;
    public string fighter1Username;
    public string fighter2UserId;
    public string fighter2Username;
    public int wager;
    public int totalPot;
    public Dictionary<string, PvPBet> bets;
}

[Serializable]
public class PvPBet
{
    public string bettorUserId;
    public string bettorUsername;
    public string fighterUsername;
    public int amount;
}

#endregion
