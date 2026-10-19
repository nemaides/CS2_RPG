using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_RPG.API;

namespace CS2_RPG.Skill.PoisonSmoke;

public class PoisonSmokeSkill : IRpgSkill
{
    public string Id => "poison_smoke";
    public string DisplayName => "Poison Smoke";
    public string Description => "Damages enemies standing inside your thrown smoke grenades over time.";
    public int DefaultMaxLevel => 10;
    public double DefaultIncrement => 10.0;

    private const float SMOKE_RADIUS_2D = 250.0f;
    private const float MAX_VERTICAL_OFFSET = 120.0f;

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
            if (playerSlot < 0) return;

            var ownerController = Utilities.GetPlayerFromSlot(playerSlot);
            if (ownerController == null || !ownerController.IsValid || ownerController.Connected != PlayerConnectedState.Connected)
                return;

            var smokeEntities = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
            if (smokeEntities == null) return;

            double effectiveIncrement = upgradeIncrement <= 0 ? 10.0 : upgradeIncrement;
            int finalDamage = Math.Max(1, (int)Math.Round(level * effectiveIncrement));

            foreach (var smoke in smokeEntities)
            {
                if (smoke == null || !smoke.IsValid) continue;
                if (!IsSmokeOwnedByPlayer(smoke, ownerController.Index)) continue;
                if (smoke.SmokeEffectTickBegin <= 0) continue;

                Vector? smokeOrigin = smoke.AbsOrigin;
                if (smokeOrigin == null) continue;

                foreach (var targetController in Utilities.GetPlayers())
                {
                    if (targetController == null || !targetController.IsValid || !targetController.PawnIsAlive) continue;

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
                        ApplySafeDamage(ownerController.Slot, targetController, targetPawn, finalDamage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[PoisonSmoke] Error in OnPeriodicTick: {ex.Message}");
        }
    }

    private static void ApplySafeDamage(int attackerSlot, CCSPlayerController targetController, CCSPlayerPawn victimPawn, int damage)
    {
        try
        {
            if (targetController == null || !targetController.IsValid || victimPawn == null || !victimPawn.IsValid || victimPawn.Health <= 0)
                return;

            int slot = targetController.Slot;
            if (PendingSuicideSlots.Contains(slot)) return;

            int currentHealth = victimPawn.Health;
            int newHealth = currentHealth - damage;

            if (newHealth <= 0)
            {
                PendingSuicideSlots.Add(slot);
                RpgPlayerActions.KillPlayer(attackerSlot, slot, "smokegrenade");
            }
            else
            {
                victimPawn.Health = newHealth;
                Utilities.SetStateChanged(victimPawn, "CBaseEntity", "m_iHealth");
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[PoisonSmoke] Error in ApplySafeDamage: {ex.Message}");
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
}