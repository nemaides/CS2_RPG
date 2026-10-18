using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.Speed;

public class SpeedSkill : IRpgSkill
{
    public string Id => "speed";
    public string DisplayName => "Speed";
    public int DefaultMaxLevel => 10;
    public double DefaultIncrement => 3;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
        if (level <= 0 || playerSlot < 0) return;

        Server.NextFrame(() =>
        {
            ApplySpeedModifier(playerSlot, level, upgradeIncrement);
        });
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement)
    {
        if (victimLevel > 0 && victimSlot >= 0)
        {
            Server.NextFrame(() =>
            {
                ApplySpeedModifier(victimSlot, victimLevel, upgradeIncrement);
            });
        }
    }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
        if (level > 0 && playerSlot >= 0)
        {
            ApplySpeedModifier(playerSlot, level, upgradeIncrement);
        }
    }

    private void ApplySpeedModifier(int playerSlot, int level, double upgradeIncrement)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return;

        float targetVelocityMod = 1.0f + (float)((level * upgradeIncrement) / 100.0);

        if (pawn.VelocityModifier < targetVelocityMod)
        {
            pawn.VelocityModifier = targetVelocityMod;
            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
        }
    }
}