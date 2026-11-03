using System.Text.Json.Serialization;

namespace CS2_RPG.Models;

public class RpgConfig
{
    [JsonPropertyOrder(-2)]
    public List<string> _Instructions { get; set; } = new()
    {
        "WeaponXP: XP awarded per kill for specific weapons.",
        "MaxUpgradeLevels: Maximum cap allowed per skill ID dynamically registered by loaded DLLs.",
        "UpgradeIncrements: Bonus multiplier/increment per skill level registered by loaded DLLs.",
        "AdminSteamIds: Comma-separated list of SteamID64 admins.",
        "Activity XP values and per-round cap control bonus objective rewards.",
        "Quest targets and XP rewards are tracked on daily and weekly UTC periods.",
        "RespecXPCost is the XP required to reset skill levels and reclaim spent points."
    };
    public List<ulong> AdminSteamIds { get; set; } = new()
    {
        // 76561198014792509
    };

    public int DefaultXP { get; set; } = 15;
    public int HeadshotBonus { get; set; } = 5;
    public double BotXpMultiplier { get; set; } = 0.1;
    public Dictionary<string, int> WeaponXP { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "knife", 25 },
        { "awp", 15 },
        { "deagle", 20 }
    };


    public Dictionary<string, int> MaxUpgradeLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, double> UpgradeIncrements { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int AssistXP { get; set; } = 5;
    public int RoundWinXP { get; set; } = 5;
    public int MvpXP { get; set; } = 5;
    public int BombPlantXP { get; set; } = 5;
    public int BombDefuseXP { get; set; } = 5;
    public int MaxBonusXPPerRound { get; set; } = 10;
    public int RespecXPCost { get; set; } = 100;

    public int DailyKillQuestTarget { get; set; } = 10;
    public int DailyKillQuestXP { get; set; } = 25;
    public int DailyHeadshotQuestTarget { get; set; } = 5;
    public int DailyHeadshotQuestXP { get; set; } = 25;
    public int WeeklyAssistQuestTarget { get; set; } = 15;
    public int WeeklyAssistQuestXP { get; set; } = 50;
    public int WeeklyWinQuestTarget { get; set; } = 5;
    public int WeeklyWinQuestXP { get; set; } = 50;
}