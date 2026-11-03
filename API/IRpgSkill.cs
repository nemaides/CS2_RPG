using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.Models;

namespace CS2_RPG.API;

public interface IRpgSkill
{
    /// <summary>Unique identifier for config/database (e.g., "speed", "health")</summary>
    string Id { get; }

    /// <summary>Display name shown in chat menus and stats</summary>
    string DisplayName { get; }

    /// <summary>Summary of what the skill does, shown in /rpghelp</summary>
    string Description { get; }

    /// <summary>Default max level if not specified in config</summary>
    int DefaultMaxLevel { get; }

    /// <summary>Default increment value if not specified in config</summary>
    double DefaultIncrement { get; }

    /// <summary>Called when a player spawns and has points in this skill</summary>
    void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement);

    /// <summary>Called when a player deals or takes damage</summary>
    void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement);

    /// <summary>Called periodically on background support ticks (if applicable)</summary>
    void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement);

    /// <summary>Returns bonus XP for planting or defusing the bomb, if supported.</summary>
    int GetObjectiveXpBonus(int level, double upgradeIncrement) => 0;

    /// <summary>Returns a multiplier for the duration of a flash effect, if supported.</summary>
    double GetBlindDurationMultiplier(int level, double upgradeIncrement) => 1.0;

    /// <summary>Called at the start of each round to reset round-limited skill effects.</summary>
    void OnRoundStart() { }
}
public static class RpgPlayerActions
{
    public static void KillPlayer(int attackerSlot, int victimSlot, string weapon = "smokegrenade")
    {
        Server.ExecuteCommand($"__rpg_internal_kill_pawn {attackerSlot} {victimSlot} {weapon}");
    }
}