using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;
using CS2_RPG.Models;
using CS2_RPG.Services;
using Microsoft.Data.Sqlite;

namespace CS2_RPG.Commands;

public static class RpgCommandHandler
{
    private static readonly Dictionary<ulong, int> _playerMenuPages = new();
    private static readonly Dictionary<ulong, DateTime> _pendingPrestigeConfirmations = new();
    private static readonly Dictionary<ulong, DateTime> _pendingRespecConfirmations = new();
    private const int PageSize = 7; 

    public static void ForgetPlayer(ulong steamId)
    {
        _playerMenuPages.Remove(steamId);
        _pendingPrestigeConfirmations.Remove(steamId);
        _pendingRespecConfirmations.Remove(steamId);
    }

    public static bool HandleChatCommand(CCSPlayerController player, string message, Dictionary<ulong, RpgPlayer> players, RpgConfig config, SqliteConnection? db, SkillManager skillManager)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        string[] parts = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        string cmd = parts[0].ToLowerInvariant();
        string[] args = parts.Skip(1).ToArray();

        if (cmd is "!rpg_commands" or "/rpg_commands")
        {
            ShowPlayerCommands(player);
            return true;
        }

        if ((cmd == "!rpg" || cmd == "/rpg") && args.Length > 0)
        {
            string subcommand = args[0].ToLowerInvariant();
            string[] subcommandArgs = args.Skip(1).ToArray();
            if (subcommand == "top")
            {
                ShowLeaderboard(player, db);
                return true;
            }
            if (subcommand == "profile")
            {
                ShowProfile(player, string.Join(' ', subcommandArgs), db, players);
                return true;
            }
            if (subcommand == "quests")
            {
                if (players.TryGetValue(player.SteamID, out var rpgPlayer))
                    RpgQuestService.ShowQuests(player, rpgPlayer, db, config);
                return true;
            }
        }

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

        if (cmd is "!quests" or "/quests" or "!rpg_quests" or "/rpg_quests")
        {
            if (players.TryGetValue(player.SteamID, out var rpgPlayer))
                RpgQuestService.ShowQuests(player, rpgPlayer, db, config);
            return true;
        }

        if (cmd is "!rpg_prestige" or "/rpg_prestige" or "!prestige" or "/prestige")
        {
            HandlePrestige(player, args, players, config, skillManager, db);
            return true;
        }

        if (cmd is "!rpg_respec" or "/rpg_respec" or "!respec" or "/respec")
        {
            HandleRespec(player, args, players, config, db, skillManager);
            return true;
        }

        if (cmd is "!rpg_title" or "/rpg_title" or "!title" or "/title")
        {
            HandleTitle(player, args, players, db);
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
                currentPage = Math.Clamp(currentPage, 0, maxPages - 1);
                _playerMenuPages[player.SteamID] = currentPage;

                if (choice >= 1 && choice <= PageSize)
                {
                    int targetIndex = (currentPage * PageSize) + (choice - 1);
                    if (targetIndex < skills.Count)
                    {
                        var selectedSkill = skills[targetIndex];
                        UpgradeSkill(player, rpgPlayer, selectedSkill.Id, config, db, skillManager);
                        ShowMenu(player, players, config, skillManager);
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

    public static void ShowPlayerCommands(CCSPlayerController? player)
    {
        if (player != null && !player.IsValid) return;

        string[] commands =
        {
            "\x04--- RPG Commands ---",
            "\x06MENU",
            "\x01/rpg \x07| \x01/rpg_menu \x07| \x01/menu \x01- Open upgrades",
            "\x01/rpg_upgrade \x07| \x01/upgrade",
            "\x06PLAYER INFO",
            "\x01/stats \x07| \x01/rpg_stats \x01- View your stats",
            "\x01/rpghelp \x07| \x01/rpg_help [skill] \x01- List skills or view details",
            "\x06COMMUNITY",
            "\x01/rpg top \x01- View the leaderboard",
            "\x01/rpg profile <name> \x01- View a player's profile",
            "\x01/quests \x07| \x01/rpg_quests \x01- View quest progress",
            "\x06CHARACTER",
            "\x01/prestige \x07| \x01/rpg_prestige [confirm] \x01- Prestige when eligible",
            "\x01/respec \x07| \x01/rpg_respec [confirm] \x01- Reset upgrades for XP",
            "\x01/title \x07| \x01/rpg_title [title] \x01- View or select a title",
            "\x06HELP",
            "\x01/rpg_commands \x01- Show this list",
            "\x01Use \x06! \x01instead of \x06/ \x01in chat. Menu: \x06/1-/7 \x01upgrade, \x06/8-/9 \x01pages."
        };

        foreach (string command in commands)
        {
            if (player == null)
                Server.PrintToConsole($"[RPG] {StripChatColors(command)}");
            else
                player.PrintToChat(command);
        }
    }

    private static string StripChatColors(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"[\x01-\x10]", string.Empty);
    }

    public static void ShowMenu(CCSPlayerController player, Dictionary<ulong, RpgPlayer> players, RpgConfig config, SkillManager skillManager)
    {
        if (player == null || !player.IsValid) return;
        if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return;

        var skills = skillManager.LoadedSkills.Values.ToList();
        int maxPages = Math.Max(1, (int)Math.Ceiling(skills.Count / (double)PageSize));
        int currentPage = _playerMenuPages.TryGetValue(player.SteamID, out int page) ? page : 0;
        currentPage = Math.Clamp(currentPage, 0, maxPages - 1);
        _playerMenuPages[player.SteamID] = currentPage;

        player.PrintToChat($"\x04--- [ RPG Upgrades | Page {currentPage + 1}/{maxPages} | Points: {rpgPlayer.SkillPoints} ] ---");
        player.PrintToChat($"\x01Type \x06/stats\x01 -> View full character stats");

        for (int i = currentPage * PageSize; i < Math.Min((currentPage + 1) * PageSize, skills.Count); i++)
        {
            var skill = skills[i];
            int currentLevel = rpgPlayer.GetSkillLevel(skill.Id);
            int maxLevel = config.MaxUpgradeLevels.TryGetValue(skill.Id, out int mCap) ? mCap : skill.DefaultMaxLevel;

            string levelStatus = (maxLevel > 0 && currentLevel >= maxLevel) ? "\x02[MAX]\x01" : $"Lvl {currentLevel}/{maxLevel}";
            int slotNumber = (i - currentPage * PageSize) + 1;

            player.PrintToChat($"\x01Type \x06/{slotNumber}\x01 -> {skill.DisplayName} ({levelStatus})");
        }

        if (skills.Count == 0)
            player.PrintToChat("\x01No RPG skills are currently loaded.");
        if (currentPage < maxPages - 1)
            player.PrintToChat("\x01Type \x06/8\x01 -> Next page");
        if (currentPage > 0)
            player.PrintToChat("\x01Type \x06/9\x01 -> Previous page");
    }

    public static void ShowStats(CCSPlayerController player, Dictionary<ulong, RpgPlayer> players, SkillManager skillManager)
    {
        if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return;

        int reqXp = rpgPlayer.GetRequiredXP();
        string title = string.IsNullOrWhiteSpace(rpgPlayer.SelectedTitle) ? string.Empty : $" \x05[{rpgPlayer.SelectedTitle}]\x01";
        player.PrintToChat($"\x04--- [ RPG Stats: {player.PlayerName} ] ---{title}");
        player.PrintToChat($"\x01Prestige: \x05{rpgPlayer.Prestige}\x01 | Level: \x06{rpgPlayer.Level}\x01 | XP: \x06{rpgPlayer.XP}/{reqXp}\x01 | Points: \x05{rpgPlayer.SkillPoints}\x01");

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

    public static void HandlePrestige(CCSPlayerController player, string[] args, Dictionary<ulong, RpgPlayer> players,
        RpgConfig config, SkillManager skillManager, SqliteConnection? db)
    {
        if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return;

        if (args.Length == 1 && args[0].Equals("confirm", StringComparison.OrdinalIgnoreCase))
        {
            if (!_pendingPrestigeConfirmations.Remove(player.SteamID, out var expiresAt) || expiresAt < DateTime.UtcNow)
            {
                player.PrintToChat(" \x02[RPG]\x01 No active prestige confirmation. Type /rpg_prestige first.");
                return;
            }

            if (!CanPrestige(rpgPlayer, config, skillManager, out string reason))
            {
                player.PrintToChat($" \x02[RPG]\x01 Prestige cancelled: {reason}");
                return;
            }

            rpgPlayer.Prestige++;
            rpgPlayer.Level = 1;
            rpgPlayer.XP = 0;
            rpgPlayer.SkillPoints = 0;
            rpgPlayer.SkillLevels.Clear();
            if (db != null) rpgPlayer.SaveToDatabase(db);

            player.PrintToChat($" \x04[RPG]\x01 Prestige {rpgPlayer.Prestige} reached! Level, XP, points, and skill upgrades have been reset.");
            ShowStats(player, players, skillManager);
            return;
        }

        if (!CanPrestige(rpgPlayer, config, skillManager, out string eligibilityReason))
        {
            player.PrintToChat($" \x02[RPG]\x01 You cannot prestige yet: {eligibilityReason}");
            return;
        }

        _pendingPrestigeConfirmations[player.SteamID] = DateTime.UtcNow.AddSeconds(30);
        player.PrintToChat(" \x04[RPG]\x01 All loaded skills are maxed. Prestige resets your level, XP, points, and upgrades.");
        player.PrintToChat("\x01Type \x06/rpg_prestige confirm\x01 within 30 seconds to continue.");
    }

    private static bool CanPrestige(RpgPlayer player, RpgConfig config, SkillManager skillManager, out string reason)
    {
        var skills = skillManager.LoadedSkills.Values.ToList();
        if (skills.Count == 0)
        {
            reason = "No skills are loaded.";
            return false;
        }

        foreach (var skill in skills)
        {
            int maxLevel = config.MaxUpgradeLevels.TryGetValue(skill.Id, out int configuredMax)
                ? configuredMax
                : skill.DefaultMaxLevel;
            if (maxLevel <= 0)
            {
                reason = $"{skill.DisplayName} has no positive maximum level configured.";
                return false;
            }
            if (player.GetSkillLevel(skill.Id) < maxLevel)
            {
                reason = $"{skill.DisplayName} must reach level {maxLevel}.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    public static void HandleRespec(CCSPlayerController player, string[] args, Dictionary<ulong, RpgPlayer> players,
        RpgConfig config, SqliteConnection? db, SkillManager skillManager)
    {
        if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return;

        if (args.Length == 1 && args[0].Equals("confirm", StringComparison.OrdinalIgnoreCase))
        {
            if (!_pendingRespecConfirmations.Remove(player.SteamID, out var expiresAt) || expiresAt < DateTime.UtcNow)
            {
                player.PrintToChat(" \x02[RPG]\x01 No active respec confirmation. Type /rpg_respec first.");
                return;
            }

            int cost = Math.Max(0, config.RespecXPCost);
            if (rpgPlayer.XP < cost)
            {
                player.PrintToChat($" \x02[RPG]\x01 You need {cost} XP to respec.");
                return;
            }

            long pointsToRefund = rpgPlayer.SkillLevels.Values.Sum(level => (long)Math.Max(0, level));
            rpgPlayer.SkillPoints = (int)Math.Min(int.MaxValue, rpgPlayer.SkillPoints + pointsToRefund);
            rpgPlayer.SkillLevels.Clear();
            rpgPlayer.XP -= cost;
            if (db != null) rpgPlayer.SaveToDatabase(db);

            player.PrintToChat($" \x04[RPG]\x01 Respec complete. Refunded {pointsToRefund} skill points for {cost} XP.");
            ShowMenu(player, players, config, skillManager);
            return;
        }

        int respecCost = Math.Max(0, config.RespecXPCost);
        if (rpgPlayer.XP < respecCost)
        {
            player.PrintToChat($" \x02[RPG]\x01 You need {respecCost} XP to respec.");
            return;
        }

        _pendingRespecConfirmations[player.SteamID] = DateTime.UtcNow.AddSeconds(30);
        player.PrintToChat($"\x04[RPG]\x01 Respec resets all skill upgrades, refunds their points, and costs {respecCost} XP.");
        player.PrintToChat("\x01Type \x06/rpg_respec confirm\x01 within 30 seconds to continue.");
    }

    public static void HandleTitle(CCSPlayerController player, string[] args, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        if (!players.TryGetValue(player.SteamID, out var rpgPlayer)) return;

        var unlockedTitles = GetUnlockedTitles(rpgPlayer.Prestige);
        if (args.Length == 0)
        {
            string available = unlockedTitles.Count == 0 ? "None yet" : string.Join(", ", unlockedTitles);
            player.PrintToChat($"\x04[RPG]\x01 Unlocked titles: {available}. Use /rpg_title <title> to select one.");
            return;
        }

        string requestedTitle = string.Join(' ', args);
        string? selectedTitle = unlockedTitles.FirstOrDefault(title => title.Equals(requestedTitle, StringComparison.OrdinalIgnoreCase));
        if (selectedTitle == null)
        {
            player.PrintToChat(" \x02[RPG]\x01 That title is not unlocked. Use /rpg_title to list your titles.");
            return;
        }

        rpgPlayer.SelectedTitle = selectedTitle;
        if (db != null) rpgPlayer.SaveToDatabase(db);
        player.PrintToChat($" \x04[RPG]\x01 Your displayed title is now \x05{selectedTitle}\x01.");
    }

    private static List<string> GetUnlockedTitles(int prestige)
    {
        var titles = new List<string>();
        if (prestige >= 1) titles.Add("Veteran");
        if (prestige >= 3) titles.Add("Elite");
        if (prestige >= 5) titles.Add("Legend");
        return titles;
    }

    public static void ShowLeaderboard(CCSPlayerController player, SqliteConnection? db)
    {
        if (db == null)
        {
            player.PrintToChat(" \x02[RPG]\x01 Leaderboard is unavailable because the database is offline.");
            return;
        }

        player.PrintToChat("\x04--- [ RPG Leaderboard | Top 5 ] ---");
        using var command = new SqliteCommand(
            "SELECT Name, Prestige, Level, XP, SelectedTitle FROM players ORDER BY Prestige DESC, Level DESC, XP DESC LIMIT 5;",
            db);
        using var reader = command.ExecuteReader();
        int rank = 1;
        while (reader.Read())
        {
            string name = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
            int prestige = reader.GetInt32(1);
            int level = reader.GetInt32(2);
            int xp = reader.GetInt32(3);
            string title = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            string titleText = string.IsNullOrWhiteSpace(title) ? string.Empty : $" [{title}]";
            player.PrintToChat($"\x01{rank}. {name}{titleText} | P{prestige} Lvl {level} | {xp} XP");
            rank++;
        }
        if (rank == 1) player.PrintToChat("\x01No player data is available yet.");
    }

    public static void ShowProfile(CCSPlayerController player, string targetName, SqliteConnection? db,
        Dictionary<ulong, RpgPlayer> players)
    {
        targetName = targetName.Trim().Trim('"', '\'').Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            player.PrintToChat(" \x02[RPG]\x01 Usage: /rpg profile <player name>");
            return;
        }

        if (db == null)
        {
            player.PrintToChat(" \x02[RPG]\x01 Player profiles are unavailable because the database is offline.");
            return;
        }

        using var command = new SqliteCommand(@"
            SELECT SteamId, Name, Prestige, Level, XP, SkillPoints, SelectedTitle
            FROM players
            WHERE Name = @name COLLATE NOCASE
               OR instr(lower(Name), lower(@name)) > 0
            ORDER BY CASE WHEN Name = @name COLLATE NOCASE THEN 0 ELSE 1 END
            LIMIT 2;", db);
        command.Parameters.AddWithValue("@name", targetName);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            player.PrintToChat($" \x02[RPG]\x01 No player found matching '{targetName}'.");
            return;
        }
        ulong steamId = unchecked((ulong)reader.GetInt64(0));
        string name = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
        int prestige = reader.GetInt32(2);
        int level = reader.GetInt32(3);
        int xp = reader.GetInt32(4);
        int points = reader.GetInt32(5);
        string title = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
        bool exactMatch = name.Equals(targetName, StringComparison.OrdinalIgnoreCase);
        if (reader.Read())
        {
            string secondName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (!exactMatch || secondName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                player.PrintToChat(" \x02[RPG]\x01 More than one player matched. Use a more specific name.");
                return;
            }
        }
        reader.Close();

        string titleText = string.IsNullOrWhiteSpace(title) ? string.Empty : $" [{title}]";
        player.PrintToChat($"\x04--- [ RPG Profile: {name}{titleText} ] ---");
        player.PrintToChat($"\x01Prestige: \x05{prestige}\x01 | Level: \x06{level}\x01 | XP: \x06{xp}\x01 | Points: \x05{points}\x01");

        var profile = players.TryGetValue(steamId, out var onlineProfile)
            ? onlineProfile
            : RpgPlayer.LoadFromDatabase(db, steamId, name);
        if (profile.SkillLevels.Count > 0)
        {
            string skillSummary = string.Join(" | ", profile.SkillLevels.Select(entry => $"{entry.Key}: {entry.Value}"));
            player.PrintToChat($"\x01Skills: {skillSummary}");
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

    public static void ShowHelp(CCSPlayerController player, string[] args, SkillManager skillManager)
    {
        if (player == null || !player.IsValid) return;

        var skillsList = skillManager.LoadedSkills.Values.ToList();

        // If a specific skill was requested (e.g., /rpghelp 4 or /rpghelp poison_smoke)
        if (args.Length > 0)
        {
            string query = args[0].Trim();
            IRpgSkill? matchingSkill = null;

            if (int.TryParse(query, out int index) && index >= 1 && index <= skillsList.Count)
            {
                matchingSkill = skillsList[index - 1];
            }
            else
            {
                matchingSkill = skillsList.FirstOrDefault(s =>
                    s.Id.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                    s.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase));
            }

            if (matchingSkill != null)
            {
                player.PrintToChat($"\x04--- [ RPG Help: {matchingSkill.DisplayName} ] ---");
                player.PrintToChat($"\x01{matchingSkill.Description}");
                player.PrintToChat("\x05" + $"Max Level:\x01 {matchingSkill.DefaultMaxLevel} | " + "\x05" + $"Bonus/Level:\x01 +{matchingSkill.DefaultIncrement}");
            }
            else
            {
                player.PrintToChat($"\x02[RPG Help]\x01 Skill '{query}' not found. Type \x06/rpghelp\x01 for the list.");
            }
            return;
        }

        // Default: Matches the exact same header and line format as the upgrade menu
        player.PrintToChat($"\x04--- [ RPG Skill Help Library ] ---");
        player.PrintToChat($"\x01Type \x06/rpghelp <number or name>\x01 -> View skill details");

        for (int i = 0; i < skillsList.Count; i++)
        {
            var skill = skillsList[i];
            int slotNumber = i + 1;

            player.PrintToChat($"\x01Type \x06{slotNumber}\x01 -> {skill.DisplayName} (\x06{skill.Id}\x01)");
        }
    }
}