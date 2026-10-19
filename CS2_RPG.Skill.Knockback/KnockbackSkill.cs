using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;
using CS2_RPG.Helpers;

namespace CS2_RPG.Skill.Knockback;

public class KnockbackSkill : IRpgSkill
{
    public string Id => "knockback";
    public string DisplayName => "Repulsion Blast";
    public string Description => "When you hit an enemy, you have a chance to knock them back, sending them flying away from you and disrupting their aim.";
    public int DefaultMaxLevel => 10;
    public double DefaultIncrement => 5.0; // 5% per level -> 50% at max level

    // --- TUNED FORCES ---
    private const float BaseKnockbackForce = 450.0f;
    private const float ForcePerLevel = 45.0f;        // 450 + (10 * 45) = 900 Force at Max Level
    private const float UpwardPushForce = 180.0f;     // Clean vertical pop to break ground friction

    // Tracks the last server tick a player received knockback to block shotgun pellet stacking
    private static readonly Dictionary<int, int> LastKnockbackTick = new();

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement) { }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement)
    {
        try
        {
            // 1. Basic slot validation
            if (attackerLevel <= 0 || attackerSlot < 0 || victimSlot < 0 || attackerSlot == victimSlot)
                return;

            // 2. Prevent shotgun pellet stacking on same tick
            int currentTick = Server.TickCount;
            if (LastKnockbackTick.TryGetValue(victimSlot, out int lastTick) && lastTick == currentTick)
            {
                return;
            }

            // 3. Controller lookup and validation
            var attacker = Utilities.GetPlayerFromSlot(attackerSlot);
            var victim = Utilities.GetPlayerFromSlot(victimSlot);

            if (attacker == null || !attacker.IsValid || attacker.PawnIsAlive == false ||
                victim == null || !victim.IsValid || victim.PawnIsAlive == false)
                return;

            // 4. Team check guard (ignore friendly fire)
            if (attacker.TeamNum == victim.TeamNum)
                return;

            // 5. Proc chance calculation
            double chance = attackerLevel * (upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement);
            if (Random.Shared.NextDouble() * 100.0 > chance)
                return;

            // 6. Pawn handle safety check
            var attackerPawn = attacker.PlayerPawn?.Value;
            var victimPawn = victim.PlayerPawn?.Value;

            if (attackerPawn == null || !attackerPawn.IsValid ||
                victimPawn == null || !victimPawn.IsValid)
                return;

            // 7. Safely check origins
            if (attackerPawn.AbsOrigin == null || victimPawn.AbsOrigin == null)
                return;

            float ax = attackerPawn.AbsOrigin.X;
            float ay = attackerPawn.AbsOrigin.Y;
            float vx = victimPawn.AbsOrigin.X;
            float vy = victimPawn.AbsOrigin.Y;

            // 8. Direction & Distance calculation
            float dirX = vx - ax;
            float dirY = vy - ay;

            float distance = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
            if (distance < 0.001f)
                return;

            float normX = dirX / distance;
            float normY = dirY / distance;

            // 9. Record tick to avoid multi-pellet processing on this frame
            LastKnockbackTick[victimSlot] = currentTick;

            // 10. Calculate total force
            float totalForce = BaseKnockbackForce + (attackerLevel * ForcePerLevel);

            // 11. Retrieve current velocity vector object safely and override directly
            var currentVel = victimPawn.AbsVelocity;
            if (currentVel != null)
            {
                currentVel.X = normX * totalForce;
                currentVel.Y = normY * totalForce;
                currentVel.Z = Math.Max(currentVel.Z, 0) + UpwardPushForce;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KnockbackSkill Error] {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement) { }
}