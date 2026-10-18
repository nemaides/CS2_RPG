using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;
using CS2_RPG.Helpers;

namespace CS2_RPG.Skill.Health;

public class HealthSkill : IRpgSkill
{
    public string Id => "health";
    public string DisplayName => "Health Boost";
    public int DefaultMaxLevel => 20;
    public double DefaultIncrement => 10.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
        if (level <= 0) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid) return;

        int maxHp = 100 + (int)(level * upgradeIncrement);
        SkillLogger.Log($"[HEALTH SKILL] Applying Health: Setting MaxHP & HP to {maxHp} for player slot {playerSlot}");

        pawn.MaxHealth = maxHp;
        pawn.Health = maxHp;

        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement) { }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement) { }
}