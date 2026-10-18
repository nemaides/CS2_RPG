using CounterStrikeSharp.API.Core;
using CS2_RPG.Models;
using CS2_RPG.Services;
using Microsoft.Data.Sqlite;

namespace CS2_RPG.Commands;

public static class RpgCommandHandler
{
    private static readonly Dictionary<ulong, int> _playerMenuPages = new();
    private const int PageSize = 7; 

    public static bool HandleChatCommand(CCSPlayerController player, string message, Dictionary<ulong, RpgPlayer> players, RpgConfig config, SqliteConnection? db, SkillManager skillManager)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        string cmd = message.Trim().ToLower();

        if (cmd == "!rpg" || cmd == "/rpg" || cmd == "!menu" || cmd == "/menu" || cmd == "!upgrade" || cmd == "/upgrade")
        {
            ShowMenu(player, players, config, skillManager);
            return true;
        }
        else if (cmd == "!stats" || cmd == "/stats")
        {
            ShowStats(player, players, skillManager);
            return true;
        }

        if (cmd.StartsWith('!') || cmd.StartsWith('/'))
        {
            string numberPart = cmd.Substring(1);
            if (int.TryParse(numberPart, out int choice))
            {
                if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return false;

                var skills = skillManager.LoadedSkills.Values.ToList();
                int currentPage = _playerMenuPages.TryGetValue(player.SteamID, out int page) ? page : 0;
                int maxPages = Math.Max(1, (int)Math.Ceiling(skills.Count / (double)PageSize));

                if (choice >= 1 && choice <= PageSize)
                {
                    int targetIndex = (currentPage * PageSize) + (choice - 1);
                    if (targetIndex < skills.Count)
                    {
                        var selectedSkill = skills[targetIndex];
                        UpgradeSkill(player, rpgPlayer, selectedSkill.Id, config, db, skillManager);
                        return true;
                    }
                }
                else if (choice == 8 && currentPage < maxPages - 1)
                {
                    _playerMenuPages[player.SteamID] = currentPage + 1;
                    ShowMenu(player, players, config, skillManager);
                    return true;
                }
                else if (choice == 9 && currentPage > 0)
                {
                    _playerMenuPages[player.SteamID] = currentPage - 1;
                    ShowMenu(player, players, config, skillManager);
                    return true;
                }
            }
        }

        return false;
    }

    public static void ShowMenu(CCSPlayerController player, Dictionary<ulong, RpgPlayer> players, RpgConfig config, SkillManager skillManager)
    {
        if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return;

        var skills = skillManager.LoadedSkills.Values.ToList();
        int totalSkills = skills.Count;
        int maxPages = Math.Max(1, (int)Math.Ceiling(totalSkills / (double)PageSize));

        if (!_playerMenuPages.TryGetValue(player.SteamID, out int currentPage) || currentPage >= maxPages)
        {
            currentPage = 0;
            _playerMenuPages[player.SteamID] = currentPage;
        }

        player.PrintToChat($"\x04--- [ RPG Upgrades (Page {currentPage + 1}/{maxPages}) | Points: {rpgPlayer.SkillPoints} ] ---");
        player.PrintToChat($"\x01Type \x06/stats\x01 -> View full character stats");

        int startIndex = currentPage * PageSize;
        int endIndex = Math.Min(startIndex + PageSize, totalSkills);

        for (int i = startIndex; i < endIndex; i++)
        {
            var skill = skills[i];
            int currentLevel = rpgPlayer.GetSkillLevel(skill.Id);
            int maxLevel = config.MaxUpgradeLevels.TryGetValue(skill.Id, out int mCap) ? mCap : skill.DefaultMaxLevel;

            string levelStatus = (maxLevel > 0 && currentLevel >= maxLevel) ? "\x02[MAX]\x01" : $"Lvl {currentLevel}/{maxLevel}";
            int slotNumber = (i % PageSize) + 1;

            player.PrintToChat($"\x01Type \x06/{slotNumber}\x01 -> {skill.DisplayName} ({levelStatus})");
        }

        if (currentPage < maxPages - 1)
        {
            player.PrintToChat($"\x01Type \x02/8\x01 -> Next Page ➡");
        }
        if (currentPage > 0)
        {
            player.PrintToChat($"\x01Type \x02/9\x01 -> ⬅ Previous Page");
        }
    }

    public static void ShowStats(CCSPlayerController player, Dictionary<ulong, RpgPlayer> players, SkillManager skillManager)
    {
        if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return;

        int reqXp = rpgPlayer.GetRequiredXP();
        player.PrintToChat($"\x04--- [ RPG Stats: {player.PlayerName} ] ---");
        player.PrintToChat($"\x01Level: \x06{rpgPlayer.Level}\x01 | XP: \x06{rpgPlayer.XP}/{reqXp}\x01 | Points: \x05{rpgPlayer.SkillPoints}\x01");

        var skillStats = new List<string>();
        foreach (var skill in skillManager.LoadedSkills.Values)
        {
            int lvl = rpgPlayer.GetSkillLevel(skill.Id);
            skillStats.Add($"{skill.DisplayName}: \x06{lvl}");
        }

        if (skillStats.Count > 0)
        {
            player.PrintToChat($"\x01" + string.Join(" | ", skillStats));
        }
    }

    public static void UpgradeSkill(CCSPlayerController player, RpgPlayer rpgPlayer, string skillId, RpgConfig config, SqliteConnection? db, SkillManager skillManager)
    {
        if (!skillManager.LoadedSkills.TryGetValue(skillId, out var skill)) return;

        if (rpgPlayer.SkillPoints <= 0)
        {
            player.PrintToChat(" \x02[RPG]\x01 You don't have enough skill points!");
            return;
        }

        int currentLevel = rpgPlayer.GetSkillLevel(skillId);
        int maxLevel = config.MaxUpgradeLevels.TryGetValue(skillId, out int mCap) ? mCap : skill.DefaultMaxLevel;

        if (maxLevel > 0 && currentLevel >= maxLevel)
        {
            player.PrintToChat($" \x02[RPG]\x01 \x06{skill.DisplayName}\x01 has reached the maximum allowed level ({maxLevel})!");
            return;
        }

        rpgPlayer.SkillPoints--;
        rpgPlayer.SetSkillLevel(skillId, currentLevel + 1);

        player.PrintToChat($" \x04[RPG]\x01 Successfully upgraded \x06{skill.DisplayName}\x01 to level {currentLevel + 1}!");

        if (db != null) rpgPlayer.SaveToDatabase(db);
    }
}