using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.RevengePop;

public class RevengePopSkill : IRpgSkill
{
    private const float BaseRadius = 180.0f;
    private const float MaxBonusRadius = 180.0f;
    private const float BaseKnockbackForce = 250.0f;
    private const float MaxBonusForce = 300.0f;
    private const float BaseUpwardForce = 90.0f;

    public string Id => "revenge_pop";
    public string DisplayName => "Revenge Pop";
    public string Description => "On death, knock back nearby enemies without dealing damage or creating smoke.";
    public int DefaultMaxLevel => 5;
    public double DefaultIncrement => 10.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement) { }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement) { }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement) { }

    public void OnPlayerDeath(int playerSlot, int level, double upgradeIncrement)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || player.IsBot || player.TeamNum is not (2 or 3)) return;

        var pawn = player.PlayerPawn?.Value;
        var origin = pawn?.AbsOrigin;
        if (pawn == null || !pawn.IsValid || origin == null) return;

        float originX = origin.X;
        float originY = origin.Y;
        float originZ = origin.Z;
        int teamNum = player.TeamNum;
        double increment = upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement;
        float radius = BaseRadius + (float)Math.Clamp(level * increment * 2.0, 0, MaxBonusRadius);
        float knockbackForce = BaseKnockbackForce + (float)Math.Clamp(level * increment * 4.0, 0, MaxBonusForce);
        float upwardForce = BaseUpwardForce + (float)Math.Clamp(level * increment, 0, 60.0);

        Server.NextFrame(() => ApplyBurst(originX, originY, originZ, teamNum, radius, knockbackForce, upwardForce));
    }

    private static void ApplyBurst(float originX, float originY, float originZ, int teamNum, float radius, float knockbackForce, float upwardForce)
    {
        foreach (var target in Utilities.GetPlayers())
        {
            if (target == null || !target.IsValid || !target.PawnIsAlive ||
                target.TeamNum is not (2 or 3) || target.TeamNum == teamNum)
                continue;

            var targetPawn = target.PlayerPawn?.Value;
            var targetOrigin = targetPawn?.AbsOrigin;
            if (targetPawn == null || !targetPawn.IsValid || targetOrigin == null || targetPawn.AbsVelocity == null) continue;

            float dx = targetOrigin.X - originX;
            float dy = targetOrigin.Y - originY;
            float dz = targetOrigin.Z - originZ;
            float distanceSquared = dx * dx + dy * dy + dz * dz;
            if (distanceSquared <= 0.001f || distanceSquared > radius * radius) continue;

            float distance = (float)Math.Sqrt(distanceSquared);
            var velocity = targetPawn.AbsVelocity;
            velocity.X = dx / distance * knockbackForce;
            velocity.Y = dy / distance * knockbackForce;
            velocity.Z = Math.Max(velocity.Z, 0) + upwardForce;
        }
    }
}
