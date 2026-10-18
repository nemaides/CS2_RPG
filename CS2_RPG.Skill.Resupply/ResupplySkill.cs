using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.Resupply;

public class ResupplySkill : IRpgSkill
{
    public string Id => "resupply";
    public string DisplayName => "Ammo Resupply";
    public int DefaultMaxLevel => 5;
    public double DefaultIncrement => 3;

    private static readonly Random _random = new();

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
        if (player == null || !player.IsValid) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0 || pawn.WeaponServices == null) return;

        var activeWeapon = pawn.WeaponServices.ActiveWeapon?.Value;
        if (activeWeapon == null || !activeWeapon.IsValid || activeWeapon.VData == null) return;

        if (activeWeapon.Clip1 < 0) return;

        double rollChance = level * upgradeIncrement;

        if (_random.NextDouble() * 100.0 < rollChance)
        {
            int maxClip = activeWeapon.VData.MaxClip1;
            if (maxClip <= 0) return;

            if (activeWeapon.Clip1 < maxClip)
            {
                activeWeapon.Clip1 += 1;
                Utilities.SetStateChanged(activeWeapon, "CBasePlayerWeapon", "m_iClip1");
            }
            else
            {
                int currentReserveClips = activeWeapon.ReserveAmmo[0];
                int maxAllowedReserveClips = 8;

                if (currentReserveClips < maxAllowedReserveClips)
                {
                    activeWeapon.ReserveAmmo[0] += 1;
                    Utilities.SetStateChanged(activeWeapon, "CBasePlayerWeapon", "m_pReserveAmmo");
                }
            }
        }
    }
}