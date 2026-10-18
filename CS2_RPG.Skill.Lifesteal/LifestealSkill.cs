using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.Lifesteal;

public class LifestealSkill : IRpgSkill
{
    public string Id => "lifesteal";
    public string DisplayName => "Lifesteal";
    public int DefaultMaxLevel => 15;
    public double DefaultIncrement => 2.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement)
    {
        if (attackerLevel <= 0 || attackerSlot < 0 || damage <= 0) return;

        var attacker = Utilities.GetPlayerFromSlot(attackerSlot);
        if (attacker == null || !attacker.IsValid) return;

        var pawn = attacker.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return;

        double lifestealPercent = (attackerLevel * upgradeIncrement) / 100.0;
        int healAmount = (int)Math.Round(damage * lifestealPercent);

        if (healAmount > 0)
        {
            int maxHp = pawn.MaxHealth > 0 ? pawn.MaxHealth : 100;

            pawn.Health = Math.Min(pawn.Health + healAmount, maxHp);

            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }
    }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
    }
}