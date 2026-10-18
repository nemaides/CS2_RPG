using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_RPG.API;

namespace CS2_RPG.Skill.PoisonSmoke;

public class PoisonSmokeSkill : IRpgSkill
{
    public string Id => "poison_smoke";
    public string DisplayName => "Poison Smoke";
    public int DefaultMaxLevel => 10;
    public double DefaultIncrement => 10.0;

    private const float SMOKE_RADIUS_2D = 250.0f;
    private const float MAX_VERTICAL_OFFSET = 120.0f;

    // Track players who already have a pending kill queued to prevent spamming frames
    private static readonly HashSet<int> PendingSuicideSlots = new();

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
        PendingSuicideSlots.Remove(playerSlot);
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement) { }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
        try
        {
            if (level <= 0 || playerSlot < 0) return;

            var ownerController = Utilities.GetPlayerFromSlot(playerSlot);
            if (ownerController == null || !ownerController.IsValid || ownerController.Connected != PlayerConnectedState.Connected) return;

            var smokeEntities = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
            if (smokeEntities == null) return;

            int finalDamage = (int)Math.Round(level * upgradeIncrement);

            foreach (var smoke in smokeEntities)
            {
                if (smoke == null || !smoke.IsValid) continue;

                if (!IsSmokeOwnedByPlayer(smoke, ownerController.Index)) continue;

                var velocity = smoke.AbsVelocity;
                if (velocity != null && velocity.Length() > 5.0f) continue;

                Vector? smokeOrigin = smoke.AbsOrigin;
                if (smokeOrigin == null) continue;

                foreach (var targetController in Utilities.GetPlayers())
                {
                    if (targetController == null || !targetController.IsValid || !targetController.PawnIsAlive) continue;

                    int targetSlot = targetController.Slot;
                    if (PendingSuicideSlots.Contains(targetSlot)) continue;

                    var targetPawn = targetController.PlayerPawn?.Value;
                    if (targetPawn == null || !targetPawn.IsValid || targetPawn.Health <= 0) continue;

                    Vector? targetOrigin = targetPawn.AbsOrigin;
                    if (targetOrigin == null) continue;

                    float dx = smokeOrigin.X - targetOrigin.X;
                    float dy = smokeOrigin.Y - targetOrigin.Y;
                    float dz = Math.Abs(smokeOrigin.Z - targetOrigin.Z);

                    float distance2D = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (distance2D <= SMOKE_RADIUS_2D && dz <= MAX_VERTICAL_OFFSET)
                    {
                        ApplySafeDamage(targetController, targetPawn, finalDamage);
                    }
                }
            }
        }
        catch
        {
            // Exception isolation
        }
    }

    private static bool IsSmokeOwnedByPlayer(CSmokeGrenadeProjectile smoke, uint ownerIndex)
    {
        try
        {
            var throwerHandle = smoke.Thrower;
            if (throwerHandle != null && throwerHandle.IsValid && throwerHandle.Value != null)
            {
                var throwerEntity = throwerHandle.Value;
                if (throwerEntity.Index == ownerIndex) return true;

                if (throwerEntity is CCSPlayerPawn throwerPawn)
                {
                    var controller = throwerPawn.Controller?.Value;
                    if (controller != null && controller.IsValid && controller.Index == ownerIndex)
                    {
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    private static void ApplySafeDamage(CCSPlayerController targetController, CCSPlayerPawn victimPawn, int damage)
    {
        if (victimPawn == null || !victimPawn.IsValid || victimPawn.Health <= 0) return;

        int currentHealth = victimPawn.Health;
        int newHealth = currentHealth - damage;
        int slot = targetController.Slot;

        if (newHealth <= 0)
        {
            // Lock this player so we only queue the kill action once
            PendingSuicideSlots.Add(slot);

            // Defer a forced engine kill execution to the next frame, bypassing health math roadblocks
            Server.NextFrame(() =>
            {
                try
                {
                    var controller = Utilities.GetPlayerFromSlot(slot);
                    if (controller != null && controller.IsValid && controller.PawnIsAlive)
                    {
                        controller.CommitSuicide(true, true);
                    }
                }
                finally
                {
                    // Cleanup handled automatically on respawn via OnPlayerSpawn
                }
            });
        }
        else
        {
            victimPawn.Health = newHealth;
            Utilities.SetStateChanged(victimPawn, "CBaseEntity", "m_iHealth");
        }
    }
}