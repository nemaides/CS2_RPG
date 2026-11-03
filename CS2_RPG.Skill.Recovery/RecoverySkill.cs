using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.Recovery;

public class RecoverySkill : IRpgSkill
{
    private const double RecoveryDelaySeconds = 9.0;
    private const int BaseHealthCap = 100;
    private const int MaxHealingPerTick = 2;
    private static readonly Dictionary<int, (ulong SteamId, DateTime LastDamageAt)> LastDamageTimes = new();

    public string Id => "recovery";
    public string DisplayName => "Recovery";
    public string Description => "Slowly restores your health after you avoid taking damage for a short time.";
    public int DefaultMaxLevel => 5;
    public double DefaultIncrement => 0.4;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
        if (playerSlot < 0) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid) return;

        LastDamageTimes[playerSlot] = (player.SteamID, DateTime.UtcNow);
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement)
    {
        if (victimLevel <= 0 || victimSlot < 0 || damage <= 0) return;

        var victim = Utilities.GetPlayerFromSlot(victimSlot);
        if (victim == null || !victim.IsValid || !victim.PawnIsAlive) return;

        LastDamageTimes[victimSlot] = (victim.SteamID, DateTime.UtcNow);
    }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
        if (level <= 0 || playerSlot < 0) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return;

        if (!LastDamageTimes.TryGetValue(playerSlot, out var damageState) || damageState.SteamId != player.SteamID)
        {
            LastDamageTimes[playerSlot] = (player.SteamID, DateTime.UtcNow);
            return;
        }

        if ((DateTime.UtcNow - damageState.LastDamageAt).TotalSeconds < RecoveryDelaySeconds) return;

        int maxHealth = Math.Min(pawn.MaxHealth > 0 ? pawn.MaxHealth : BaseHealthCap, BaseHealthCap);
        if (pawn.Health >= maxHealth) return;

        double increment = upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement;
        int healAmount = Math.Clamp((int)Math.Ceiling(level * increment), 1, MaxHealingPerTick);
        pawn.Health = Math.Min(pawn.Health + healAmount, maxHealth);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
    }
}
