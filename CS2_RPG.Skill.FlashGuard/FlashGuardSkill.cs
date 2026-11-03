using CS2_RPG.API;

namespace CS2_RPG.Skill.FlashGuard;

public class FlashGuardSkill : IRpgSkill
{
    private const double MaxReductionPercent = 10.0;

    public string Id => "flash_guard";
    public string DisplayName => "Flash Guard";
    public string Description => "Reduces the duration of flash effects on you by a small amount.";
    public int DefaultMaxLevel => 5;
    public double DefaultIncrement => 2.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement) { }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement) { }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement) { }

    public double GetBlindDurationMultiplier(int level, double upgradeIncrement)
    {
        if (level <= 0) return 1.0;

        double increment = upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement;
        double reduction = Math.Clamp(level * increment, 0, MaxReductionPercent);
        return 1.0 - (reduction / 100.0);
    }
}
