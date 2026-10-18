using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;
using CS2_RPG.Helpers;

namespace CS2_RPG.Skill.Medic;

public class MedicSkill : IRpgSkill
{
    public string Id => "medic";
    public string DisplayName => "Medic Aura";
    public int DefaultMaxLevel => 10;
    public double DefaultIncrement => 1.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement)
    {
    }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
        if (level <= 0 || playerSlot < 0) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0 || pawn.AbsOrigin == null) return;

        var myOrigin = pawn.AbsOrigin;
        //bool healedSomeone = false;

        int healAmount = (int)Math.Round(level * upgradeIncrement);

        foreach (var target in Utilities.GetPlayers())
        {
            if (target == null || !target.IsValid || target == player) continue;
            if (!target.PawnIsAlive || target.TeamNum != player.TeamNum) continue;

            var targetPawn = target.PlayerPawn?.Value;
            if (targetPawn == null || !targetPawn.IsValid || targetPawn.Health <= 0) continue;

            var targetOrigin = targetPawn.AbsOrigin;
            if (targetOrigin == null) continue;

            float distance = (myOrigin - targetOrigin).Length();

            if (distance <= 300.0f)
            {
                int maxHp = targetPawn.MaxHealth > 0 ? targetPawn.MaxHealth : 100;

                if (targetPawn.Health < maxHp)
                {
                    targetPawn.Health = Math.Min(targetPawn.Health + healAmount, maxHp);
                    Utilities.SetStateChanged(targetPawn, "CBaseEntity", "m_iHealth");
                    //healedSomeone = true;
                }
            }
        }

        //if (healedSomeone && pawn.AbsOrigin != null)
        //{
        //    VisualEffects.SpawnGroundAura(pawn.AbsOrigin, "particles/inferno_fx/inferno_extinguish_ring.vpcf");
        //}
    }
}