using CS2_RPG.API;

namespace CS2_RPG.Skill.ObjectiveSpecialist;

public class ObjectiveSpecialistSkill : IRpgSkill
{
    private const int MaxBonusXp = 10;

    public string Id => "objective_specialist";
    public string DisplayName => "Objective Specialist";
    public string Description => "Earns a small bonus XP reward for planting or defusing the bomb.";
    public int DefaultMaxLevel => 5;
    public double DefaultIncrement => 2.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement) { }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement) { }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement) { }

    public int GetObjectiveXpBonus(int level, double upgradeIncrement)
    {
        if (level <= 0) return 0;

        double increment = upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement;
        return Math.Clamp((int)Math.Round(level * increment), 0, MaxBonusXp);
    }
}
