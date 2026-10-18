using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CS2_RPG.Models;
using Microsoft.Data.Sqlite;

namespace CS2_RPG.Commands;

public static class AdminCommands
{
    public static List<CCSPlayerController> FindPlayers(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return new List<CCSPlayerController>();

        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).ToList();


        var exact = players.Where(p => p.PlayerName.Equals(targetName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0) return exact;


        return players.Where(p => p.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static void ShowAdminHelp(CCSPlayerController? player, RpgConfig config)
    {
        if (player != null && player.IsValid)
        {
            if (!config.AdminSteamIds.Contains(player.SteamID))
            {
                player.PrintToChat(" \x02[RPG]\x01 You are not authorized to view admin commands.");
                return;
            }

            player.PrintToChat("\x04--- [ RPG Admin Commands ] ---");
            player.PrintToChat("\x01Type \x06/rpg_reset <name>\x01 -> Reset player stats");
            player.PrintToChat("\x01Type \x06/rpg_addlevel <name> <amount>\x01 -> Add levels");
            player.PrintToChat("\x01Type \x06/rpg_setlevel <name> <level>\x01 -> Set exact level");
            player.PrintToChat("\x01" + "Console: \x06rpg_reset\x01 | \x06rpg_addlevel\x01 | \x06rpg_setlevel\x01");
        }
        else
        {
            Server.PrintToConsole("[RPG Admin Commands]");
            Server.PrintToConsole(" - rpg_reset <player_name>");
            Server.PrintToConsole(" - rpg_addlevel <player_name> <amount>");
            Server.PrintToConsole(" - rpg_setlevel <player_name> <level>");
        }
    }

    // Console Command Wrapper
    public static void ResetPlayerStats(CCSPlayerController? player, CommandInfo command, RpgConfig config, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        string targetName = command.GetArg(1);
        ResetPlayerStatsByName(player, targetName, config, players, db);
    }

    public static void ResetPlayerStatsByName(CCSPlayerController? player, string targetName, RpgConfig config, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        if (player != null && player.IsValid && !config.AdminSteamIds.Contains(player.SteamID))
        {
            player.PrintToChat(" \x02[RPG]\x01 You are not authorized to use admin commands.");
            return;
        }

        if (string.IsNullOrEmpty(targetName) && player != null) targetName = player.PlayerName;

        var matches = FindPlayers(targetName);

        if (matches.Count == 0)
        {
            SendMsg(player, $" \x02[RPG]\x01 No players found matching '{targetName}'.");
            return;
        }

        if (matches.Count > 1 && player != null)
        {
            var menu = new ChatMenu($"[RPG] Multiple players match '{targetName}':");
            foreach (var target in matches)
            {
                menu.AddMenuOption(target.PlayerName, (admin, option) =>
                {
                    ExecuteReset(admin, target, players, db);
                });
            }
            MenuManager.OpenChatMenu(player, menu);
            return;
        }

        ExecuteReset(player, matches[0], players, db);
    }

    private static void ExecuteReset(CCSPlayerController? admin, CCSPlayerController target, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        if (players.TryGetValue(target.SteamID, out var rpgTarget))
        {
            rpgTarget.Level = 1;
            rpgTarget.XP = 0;
            rpgTarget.SkillPoints = 0;
            rpgTarget.SkillLevels.Clear(); // Clears all dynamic modular skills[cite: 1]

            if (db != null) rpgTarget.SaveToDatabase(db);

            target.PrintToChat(" \x04[RPG]\x01 Your character stats have been reset by an administrator.");

            if (admin != null && admin != target)
                admin.PrintToChat($" \x04[RPG]\x01 Successfully reset stats for \x06{target.PlayerName}\x01.");
            else if (admin == null)
                Server.PrintToConsole($"[RPG] Successfully reset stats for {target.PlayerName}.");
        }
    }

    // Console Command Wrapper
    public static void AddPlayerLevels(CCSPlayerController? player, CommandInfo command, RpgConfig config, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        string targetName = command.GetArg(1);
        int amount = int.TryParse(command.GetArg(2), out int parsed) ? parsed : 1;
        AddPlayerLevelsByName(player, targetName, amount, config, players, db);
    }

    public static void AddPlayerLevelsByName(CCSPlayerController? player, string targetName, int levelsToAdd, RpgConfig config, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        if (player != null && player.IsValid && !config.AdminSteamIds.Contains(player.SteamID))
        {
            player.PrintToChat(" \x02[RPG]\x01 You are not authorized to use admin commands.");
            return;
        }

        if (string.IsNullOrEmpty(targetName) && player != null) targetName = player.PlayerName;

        var matches = FindPlayers(targetName);

        if (matches.Count == 0)
        {
            SendMsg(player, $" \x02[RPG]\x01 No players found matching '{targetName}'.");
            return;
        }

        if (matches.Count > 1 && player != null)
        {
            var menu = new ChatMenu($"[RPG] Multiple players match '{targetName}':");
            foreach (var target in matches)
            {
                menu.AddMenuOption(target.PlayerName, (admin, option) =>
                {
                    ExecuteAddLevel(admin, target, levelsToAdd, players, db);
                });
            }
            MenuManager.OpenChatMenu(player, menu);
            return;
        }

        ExecuteAddLevel(player, matches[0], levelsToAdd, players, db);
    }

    private static void ExecuteAddLevel(CCSPlayerController? admin, CCSPlayerController target, int levelsToAdd, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        if (players.TryGetValue(target.SteamID, out var rpgTarget))
        {
            rpgTarget.Level += levelsToAdd;
            rpgTarget.SkillPoints += levelsToAdd;

            if (db != null) rpgTarget.SaveToDatabase(db);

            target.PrintToChat($" \x04[RPG]\x01 An administrator granted you \x06+{levelsToAdd} Level(s)\x01! You are now \x04Level {rpgTarget.Level}\x01.");

            if (admin != null && admin != target)
                admin.PrintToChat($" \x04[RPG]\x01 Successfully granted +{levelsToAdd} level(s) to \x06{target.PlayerName}\x01.");
            else if (admin == null)
                Server.PrintToConsole($"[RPG] Granted +{levelsToAdd} level(s) to {target.PlayerName}.");
        }
    }

    // Console Command Wrapper
    public static void SetPlayerLevel(CCSPlayerController? player, CommandInfo command, RpgConfig config, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        string targetName = command.GetArg(1);
        int targetLevel = int.TryParse(command.GetArg(2), out int parsed) ? parsed : 1;
        SetPlayerLevelByName(player, targetName, targetLevel, config, players, db);
    }

    public static void SetPlayerLevelByName(CCSPlayerController? player, string targetName, int targetLevel, RpgConfig config, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        if (player != null && player.IsValid && !config.AdminSteamIds.Contains(player.SteamID))
        {
            player.PrintToChat(" \x02[RPG]\x01 You are not authorized to use admin commands.");
            return;
        }

        if (string.IsNullOrEmpty(targetName) && player != null) targetName = player.PlayerName;

        var matches = FindPlayers(targetName);

        if (matches.Count == 0)
        {
            SendMsg(player, $" \x02[RPG]\x01 No players found matching '{targetName}'.");
            return;
        }

        if (matches.Count > 1 && player != null)
        {
            var menu = new ChatMenu($"[RPG] Multiple players match '{targetName}':");
            foreach (var target in matches)
            {
                menu.AddMenuOption(target.PlayerName, (admin, option) =>
                {
                    ExecuteSetLevel(admin, target, targetLevel, players, db);
                });
            }
            MenuManager.OpenChatMenu(player, menu);
            return;
        }

        ExecuteSetLevel(player, matches[0], targetLevel, players, db);
    }

    private static void ExecuteSetLevel(CCSPlayerController? admin, CCSPlayerController target, int targetLevel, Dictionary<ulong, RpgPlayer> players, SqliteConnection? db)
    {
        if (players.TryGetValue(target.SteamID, out var rpgTarget))
        {
            int levelDifference = targetLevel - rpgTarget.Level;
            rpgTarget.Level = targetLevel;
            rpgTarget.SkillPoints = Math.Max(0, rpgTarget.SkillPoints + levelDifference);

            if (db != null) rpgTarget.SaveToDatabase(db);

            target.PrintToChat($" \x04[RPG]\x01 An administrator set your level to \x04Level {rpgTarget.Level}\x01.");

            if (admin != null && admin != target)
                admin.PrintToChat($" \x04[RPG]\x01 Successfully set level for \x06{target.PlayerName}\x01 to \x04Level {targetLevel}\x01.");
            else if (admin == null)
                Server.PrintToConsole($"[RPG] Set level for {target.PlayerName} to Level {targetLevel}.");
        }
    }

    private static void SendMsg(CCSPlayerController? player, string message)
    {
        if (player != null) player.PrintToChat(message);
        else Server.PrintToConsole(message);
    }
}