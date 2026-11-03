using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.Clutch;

public class ClutchSkill : IRpgSkill
{
    private const double MaxBonusPercent = 5.0;
    private const float MaxVelocityModifier = 1.15f;
    private static readonly TimeSpan BoostDuration = TimeSpan.FromSeconds(3);
    private static readonly HashSet<ulong> UsedThisRound = new();
    private static readonly Dictionary<ulong, ActiveBoost> ActiveBoosts = new();

    private readonly record struct ActiveBoost(int PlayerSlot, float PreviousModifier, float AppliedModifier, DateTime ExpiresAt);

    public string Id => "clutch";
    public string DisplayName => "Clutch";
    public string Description => "Once per round, gain a brief movement boost when you are your team's last survivor.";
    public int DefaultMaxLevel => 5;
    public double DefaultIncrement => 1.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement) { }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement) { }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
        if (level <= 0 || playerSlot < 0) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return;

        ulong steamId = player.SteamID;
        DateTime now = DateTime.UtcNow;

        if (ActiveBoosts.TryGetValue(steamId, out var activeBoost))
        {
            if (now < activeBoost.ExpiresAt) return;

            if (Math.Abs(pawn.VelocityModifier - activeBoost.AppliedModifier) < 0.001f)
            {
                pawn.VelocityModifier = activeBoost.PreviousModifier;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
            }

            ActiveBoosts.Remove(steamId);
        }

        if (UsedThisRound.Contains(steamId)) return;

        int teammatesAlive = 0;
        int opponentsAlive = 0;
        foreach (var other in Utilities.GetPlayers())
        {
            if (other == null || !other.IsValid || !other.PawnIsAlive) continue;

            if (other.TeamNum == player.TeamNum)
                teammatesAlive++;
            else if (other.TeamNum is 2 or 3)
                opponentsAlive++;
        }

        if (teammatesAlive != 1 || opponentsAlive == 0) return;

        double increment = upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement;
        double bonusPercent = Math.Clamp(level * increment, 0, MaxBonusPercent);
        UsedThisRound.Add(steamId);

        Server.NextFrame(() => ApplyBoost(playerSlot, steamId, (float)bonusPercent, now));
    }

    public void OnRoundStart()
    {
        foreach (var entry in ActiveBoosts)
        {
            var player = Utilities.GetPlayerFromSlot(entry.Value.PlayerSlot);
            if (player == null || !player.IsValid || player.SteamID != entry.Key) continue;

            var pawn = player.PlayerPawn?.Value;
            if (pawn == null || !pawn.IsValid) continue;

            if (Math.Abs(pawn.VelocityModifier - entry.Value.AppliedModifier) < 0.001f)
            {
                pawn.VelocityModifier = entry.Value.PreviousModifier;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
            }
        }

        ActiveBoosts.Clear();
        UsedThisRound.Clear();
    }

    private static void ApplyBoost(int playerSlot, ulong steamId, float bonusPercent, DateTime triggeredAt)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || !player.PawnIsAlive || player.SteamID != steamId) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return;

        float currentModifier = pawn.VelocityModifier > 0 ? pawn.VelocityModifier : 1.0f;
        float boostedModifier = Math.Min(currentModifier + (bonusPercent / 100.0f), MaxVelocityModifier);
        if (boostedModifier <= currentModifier) return;

        pawn.VelocityModifier = boostedModifier;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
        ActiveBoosts[steamId] = new ActiveBoost(playerSlot, currentModifier, boostedModifier, triggeredAt + BoostDuration);
    }
}
