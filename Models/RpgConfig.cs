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
        "AdminSteamIds: Comma-separated list of SteamID64 admins."
    };
    public List<ulong> AdminSteamIds { get; set; } = new()
    {
        // 76561198014792509
    };

    public int DefaultXP { get; set; } = 25;
    public int HeadshotBonus { get; set; } = 15;
    public double BotXpMultiplier { get; set; } = 0.25;
    public Dictionary<string, int> WeaponXP { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "knife", 130 },
        { "awp", 25 },
        { "deagle", 40 }
    };


    public Dictionary<string, int> MaxUpgradeLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, double> UpgradeIncrements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}