using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_RPG.API;
using CS2_RPG.Commands;
using CS2_RPG.Helpers;
using CS2_RPG.Models;
using CS2_RPG.Services;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace CS2_RPG;

public class CS2_RPG : BasePlugin
{
    public override string ModuleName => "CS2 RPG Mod";
    public override string ModuleVersion => "0.3.2";
    public override string ModuleAuthor => "Nemaides";

    private readonly Dictionary<ulong, RpgPlayer> _rpgPlayers = new();
    private readonly SkillManager _skillManager = new();
    private SqliteConnection? _dbConnection;
    private RpgConfig _config = new();

    private string ConfigPath
    {
        get
        {
            string configsDir = Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", "CS2_RPG");

            if (!Directory.Exists(configsDir))
            {
                Directory.CreateDirectory(configsDir);
            }

            return Path.Combine(configsDir, "rpg_config.json");
        }
    }

    private string LogsDirectory
    {
        get
        {
            string logsDir = Path.Combine(ModuleDirectory, "..", "..", "logs", "CS2_RPG");

            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            return logsDir;
        }
    }

    private readonly HashSet<ulong> _connectedPlayers = new();

    public override void Load(bool hotReload)
    {
        SkillLogger.Initialize(LogsDirectory);
        SkillLogger.Log("=== CS2 RPG Plugin Initializing ===");

        Console.WriteLine("=========================================");
        Console.WriteLine($"[CS2 RPG] Initializing Modular RPG v{ModuleVersion}...");
        Console.WriteLine("=========================================");

        // Register a hidden command directly without [ConsoleCommand] attributes
        AddCommand("__rpg_internal_kill_pawn", "Internal handler for RPG player deaths", (caller, command) =>
        {
            // Strictly block players/clients from running this command manually
            if (caller != null) return;

            if (command.ArgCount >= 3 &&
                int.TryParse(command.GetArg(1), out int attackerSlot) &&
                int.TryParse(command.GetArg(2), out int victimSlot))
            {
                string weapon = command.ArgCount >= 4 ? command.GetArg(3) : "smokegrenade";

                var victim = Utilities.GetPlayerFromSlot(victimSlot);
                var attacker = Utilities.GetPlayerFromSlot(attackerSlot);

                if (victim != null && victim.IsValid && victim.PawnIsAlive)
                {
                    var victimPawn = victim.PlayerPawn?.Value;
                    if (victimPawn != null && victimPawn.IsValid)
                    {
                        if (attacker != null && attacker.IsValid)
                        {
                            // 1. Increment the scoreboard kill stats for the attacker
                            if (attacker.ActionTrackingServices != null)
                            {
                                attacker.ActionTrackingServices.MatchStats.Kills++;
                            }

                            // 2. Broadcast the killfeed event
                            var deathEvent = new EventPlayerDeath(false)
                            {
                                Userid = victim,
                                Attacker = attacker,
                                Weapon = weapon,
                                DmgHealth = 100,
                                Headshot = false
                            };
                            deathEvent.FireEvent(false);
                        }

                        // 3. Clear health and clean up the dead pawn entity
                        victimPawn.Health = 0;
                        Utilities.SetStateChanged(victimPawn, "CBaseEntity", "m_iHealth");
                        victimPawn.AcceptInput("Kill", null, null, "");
                    }
                }
            }
        });
        LoadConfig();

        try
        {
            _dbConnection = DatabaseHelper.InitDatabase(ModuleDirectory);
        }
        catch (Exception ex)
        {
            string err = $"[CS2 RPG ERROR] DB Init failed: {ex.Message}";
            Console.WriteLine(err);
            SkillLogger.Log(err);
        }

        _skillManager.LoadAllSkills(ModuleDirectory, _config);
        Console.WriteLine($"[CS2 RPG] Total skills recognized by SkillManager: {_skillManager.LoadedSkills.Count}");
        SkillLogger.Log($"Total skills recognized by SkillManager: {_skillManager.LoadedSkills.Count}");

        if (_config.MaxUpgradeLevels == null)
            _config.MaxUpgradeLevels = new(StringComparer.OrdinalIgnoreCase);

        if (_config.UpgradeIncrements == null)
            _config.UpgradeIncrements = new(StringComparer.OrdinalIgnoreCase);

        bool configChanged = false;
        foreach (var kvp in _skillManager.LoadedSkills)
        {
            string skillId = kvp.Key.ToLowerInvariant();
            var skill = kvp.Value;

            if (!_config.MaxUpgradeLevels.ContainsKey(skillId))
            {
                _config.MaxUpgradeLevels[skillId] = skill.DefaultMaxLevel;
                configChanged = true;
                Console.WriteLine($"[Config Sync] Added MaxUpgradeLevels for '{skillId}' -> {skill.DefaultMaxLevel}");
                SkillLogger.Log($"[Config Sync] Added missing MaxUpgradeLevel for skill ID: {skillId}");
            }

            if (!_config.UpgradeIncrements.ContainsKey(skillId))
            {
                _config.UpgradeIncrements[skillId] = skill.DefaultIncrement;
                configChanged = true;
                Console.WriteLine($"[Config Sync] Added UpgradeIncrements for '{skillId}' -> {skill.DefaultIncrement}");
                SkillLogger.Log($"[Config Sync] Added missing UpgradeIncrement for skill ID: {skillId}");
            }
        }

        if (configChanged)
        {
            SaveConfigToDisk();
            Console.WriteLine("[CS2 RPG] rpg_config.json successfully populated with new skill defaults!");
            SkillLogger.Log("rpg_config.json successfully populated with new skill defaults.");
        }

        AddTimer(3.0f, () =>
        {
            foreach (var kvp in _rpgPlayers)
            {
                var player = Utilities.GetPlayerFromSteamId(kvp.Key);
                if (player == null || !player.IsValid || !player.PawnIsAlive) continue;

                foreach (var skill in _skillManager.LoadedSkills.Values)
                {
                    int level = kvp.Value.GetSkillLevel(skill.Id);
                    if (level > 0)
                    {
                        double inc = _config.UpgradeIncrements.TryGetValue(skill.Id, out double val) ? val : skill.DefaultIncrement;
                        skill.OnPeriodicTick(player.Slot, level, inc);
                    }
                }
            }
        }, TimerFlags.REPEAT);
        
        AddCommandListener("say", OnPlayerChat, HookMode.Pre);
        AddCommandListener("say_team", OnPlayerChat, HookMode.Pre);

        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot) continue;

                ulong steamId = player.SteamID;
                if (_dbConnection != null)
                    _rpgPlayers[steamId] = RpgPlayer.LoadFromDatabase(_dbConnection, steamId, player.PlayerName);
                else
                    _rpgPlayers[steamId] = new RpgPlayer(steamId, player.PlayerName);

                player.PrintToChat(" \x04[RPG]\x01 Plugin reloaded! Progress restored.");
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        SkillLogger.Log("=== CS2 RPG Plugin Unloading ===");
        foreach (var rpgPlayer in _rpgPlayers.Values)
        {
            if (_dbConnection != null) rpgPlayer.SaveToDatabase(_dbConnection);
        }
        _dbConnection?.Close();
        _dbConnection?.Dispose();
    }

    private void LoadConfig()
    {
        var defaultConfig = new RpgConfig();

        if (File.Exists(ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                _config = JsonSerializer.Deserialize<RpgConfig>(json, options) ?? new RpgConfig();

                bool configUpdated = false;

                if (_config._Instructions == null || !_config._Instructions.Any())
                {
                    _config._Instructions = defaultConfig._Instructions;
                    configUpdated = true;
                }

                if (configUpdated)
                {
                    SaveConfigToDisk();
                }
            }
            catch (Exception ex)
            {
                string err = $"[CS2 RPG ERROR] Config load failed: {ex.Message}";
                Console.WriteLine(err);
                SkillLogger.Log(err);
                if (_config == null) _config = defaultConfig;
            }
        }
        else
        {
            _config = defaultConfig;
            SaveConfigToDisk();
        }
    }

    private void SaveConfigToDisk()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            string err = $"[CS2 RPG ERROR] Failed to save config to disk: {ex.Message}";
            Console.WriteLine(err);
            SkillLogger.Log(err);
        }
    }

    private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return HookResult.Continue;

        string rawMessage = info.ArgString.Trim().Trim('"', '\'').Trim();
        bool handled = RpgCommandHandler.HandleChatCommand(player, rawMessage, _rpgPlayers, _config, _dbConnection, _skillManager);

        return handled ? HookResult.Handled : HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        ulong steamId = player.SteamID;

        if (_connectedPlayers.Contains(steamId)) return HookResult.Continue;
        _connectedPlayers.Add(steamId);

        if (_dbConnection != null)
            _rpgPlayers[steamId] = RpgPlayer.LoadFromDatabase(_dbConnection, steamId, player.PlayerName);
        else if (!_rpgPlayers.ContainsKey(steamId))
            _rpgPlayers[steamId] = new RpgPlayer(steamId, player.PlayerName);

        player.PrintToChat($" \x04[RPG]\x01 Welcome! Type \x06/stats\x01 to view progress, \x06/rpg\x01 for upgrades or \x06/rpghelp\x01 to see skill descriptions.");
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        ulong steamId = player.SteamID;
        _connectedPlayers.Remove(steamId);

        if (_rpgPlayers.TryGetValue(steamId, out var rpgPlayer))
        {
            if (_dbConnection != null) rpgPlayer.SaveToDatabase(_dbConnection);
            _rpgPlayers.Remove(steamId);
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        ulong steamId = player.SteamID;
        int slot = player.Slot;

        Server.NextFrame(() =>
        {
            var targetPlayer = Utilities.GetPlayerFromSlot(slot);
            if (targetPlayer == null || !targetPlayer.IsValid || !targetPlayer.PawnIsAlive) return;

            if (_rpgPlayers.TryGetValue(steamId, out var rpgPlayer))
            {
                foreach (var skill in _skillManager.LoadedSkills.Values)
                {
                    int level = rpgPlayer.GetSkillLevel(skill.Id);
                    if (level > 0)
                    {
                        double inc = _config.UpgradeIncrements.TryGetValue(skill.Id, out double val) ? val : skill.DefaultIncrement;
                        skill.OnPlayerSpawn(slot, level, inc);
                    }
                }
            }
        });

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var victim = @event.Userid;

        if (attacker == null || !attacker.IsValid || attacker.IsBot || victim == null || attacker == victim)
            return HookResult.Continue;

        if (_rpgPlayers.TryGetValue(attacker.SteamID, out var rpgAttacker))
        {
            string rawWeapon = (@event.Weapon ?? "unknown").ToLower();
            string lookupWeapon = (rawWeapon.Contains("knife") || rawWeapon.Contains("bayonet")) ? "knife" : rawWeapon;

            double xpEarned = _config.WeaponXP.TryGetValue(lookupWeapon, out int customXp) ? customXp : _config.DefaultXP;

            if (@event.Headshot) xpEarned += _config.HeadshotBonus;
            bool isBotKill = victim.IsBot;
            if (isBotKill) xpEarned *= _config.BotXpMultiplier;

            int finalXp = (int)Math.Round(xpEarned);
            rpgAttacker.AddXP(finalXp);
            int reqXp = rpgAttacker.GetRequiredXP();

            string weaponDisplayName = WeaponHelper.GetCleanWeaponName(rawWeapon);
            string botTag = isBotKill ? " (Bot)" : "";

            attacker.PrintToChat($" \x04[RPG]\x01 +{finalXp} XP for killing {victim.PlayerName} with \x05{weaponDisplayName}{botTag}\x01 | \x06XP: {rpgAttacker.XP}/{reqXp}\x01 | \x05Points: {rpgAttacker.SkillPoints}\x01");

            if (_dbConnection != null) rpgAttacker.SaveToDatabase(_dbConnection);
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var victim = @event.Userid;

        CCSPlayerController? attackerCtrl = (attacker != null && attacker.IsValid) ? attacker : null;
        CCSPlayerController? victimCtrl = (victim != null && victim.IsValid) ? victim : null;

        RpgPlayer? attackerRpg = (attackerCtrl != null && _rpgPlayers.TryGetValue(attackerCtrl.SteamID, out var aRpg)) ? aRpg : null;
        RpgPlayer? victimRpg = (victimCtrl != null && _rpgPlayers.TryGetValue(victimCtrl.SteamID, out var vRpg)) ? vRpg : null;

        foreach (var skill in _skillManager.LoadedSkills.Values)
        {
            int attackerLevel = attackerRpg?.GetSkillLevel(skill.Id) ?? 0;
            int victimLevel = victimRpg?.GetSkillLevel(skill.Id) ?? 0;

            if (attackerLevel > 0 || victimLevel > 0)
            {
                int attackerSlot = attackerCtrl?.Slot ?? -1;
                int victimSlot = victimCtrl?.Slot ?? -1;
                double inc = _config.UpgradeIncrements.TryGetValue(skill.Id, out double val) ? val : skill.DefaultIncrement;
                skill.OnPlayerHurt(attackerSlot, victimSlot, @event.DmgHealth, attackerLevel, victimLevel, inc);
            }
        }

        return HookResult.Continue;
    }

    [ConsoleCommand("rpg_rpg", "Open RPG Menu")]
    [ConsoleCommand("rpg_menu", "Open RPG Menu")]
    [ConsoleCommand("rpg_upgrade", "Open RPG Menu")]
    public void CmdRpg(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;
        RpgCommandHandler.ShowMenu(player, _rpgPlayers, _config, _skillManager);
    }

    [ConsoleCommand("rpg_stats", "View stats")]
    public void CmdStats(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;
        RpgCommandHandler.ShowStats(player, _rpgPlayers, _skillManager);
    }

    [ConsoleCommand("rpg_admin", "View RPG admin commands")]
    public void CmdAdminHelp(CCSPlayerController? p, CommandInfo c) { AdminCommands.ShowAdminHelp(p, _config); }

    [ConsoleCommand("rpg_reset", "Reset player stats")]
    public void CmdReset(CCSPlayerController? p, CommandInfo c) { AdminCommands.ResetPlayerStats(p, c, _config, _rpgPlayers, _dbConnection); }

    [ConsoleCommand("rpg_addlevel", "Add levels to a player")]
    public void CmdAddLevel(CCSPlayerController? p, CommandInfo c) { AdminCommands.AddPlayerLevels(p, c, _config, _rpgPlayers, _dbConnection); }

    [ConsoleCommand("rpg_setlevel", "Set exact level for a player")]
    public void CmdSetLevel(CCSPlayerController? p, CommandInfo c) { AdminCommands.SetPlayerLevel(p, c, _config, _rpgPlayers, _dbConnection); }

    [ConsoleCommand("rpg_help", "View RPG skill descriptions")]
    [ConsoleCommand("rpghelp", "View RPG skill descriptions")]
    public void CmdRpgHelp(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        // Extract arguments if any were passed after /rpghelp
        string[] args = new string[command.ArgCount - 1];
        for (int i = 1; i < command.ArgCount; i++)
        {
            args[i - 1] = command.GetArg(i);
        }

        RpgCommandHandler.ShowHelp(player, args, _skillManager);
    }
}