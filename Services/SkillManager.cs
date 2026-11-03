using System.Reflection;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;
using CS2_RPG.Helpers;
using CS2_RPG.Models;

namespace CS2_RPG.Services;

public class SkillManager
{
    public Dictionary<string, IRpgSkill> LoadedSkills { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void LoadAllSkills(string pluginDirectory, RpgConfig config)
    {
        LoadedSkills.Clear();

        string skillsDir = Path.Combine(pluginDirectory, "Skills");
        if (!Directory.Exists(skillsDir))
        {
            Directory.CreateDirectory(skillsDir);
            SkillLogger.Log("Created 'Skills' directory for modular skill DLLs.");
            return;
        }

        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var requestedName = new AssemblyName(args.Name).Name;

            if (string.Equals(requestedName, "CS2_RPG", StringComparison.OrdinalIgnoreCase))
            {
                string coreDll = Path.Combine(pluginDirectory, "CS2_RPG.dll");
                if (File.Exists(coreDll)) return Assembly.LoadFrom(coreDll);
            }

            if (string.Equals(requestedName, "CounterStrikeSharp.API", StringComparison.OrdinalIgnoreCase))
            {
                string cssDll = Path.Combine(pluginDirectory, "CounterStrikeSharp.API.dll");
                if (File.Exists(cssDll)) return Assembly.LoadFrom(cssDll);

                var existing = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "CounterStrikeSharp.API", StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing;
            }

            return null;
        };

        foreach (var dllPath in Directory.GetFiles(skillsDir, "*.dll"))
        {
            try
            {
                SkillLogger.Log($"Scanning DLL: {Path.GetFileName(dllPath)}");
                var assembly = Assembly.LoadFrom(dllPath);

                foreach (var type in assembly.GetTypes())
                {
                    bool hasInterface = type.GetInterfaces().Any(i => i.Name == "IRpgSkill");
                    if (hasInterface && !type.IsInterface && !type.IsAbstract)
                    {
                        try
                        {
                            var instance = Activator.CreateInstance(type);
                            if (instance != null)
                            {
                                var wrappedSkill = new ReflectionSkillWrapper(instance);
                                LoadedSkills[wrappedSkill.Id] = wrappedSkill;

                                if (!config.MaxUpgradeLevels.ContainsKey(wrappedSkill.Id))
                                {
                                    config.MaxUpgradeLevels[wrappedSkill.Id] = wrappedSkill.DefaultMaxLevel;
                                }
                                if (!config.UpgradeIncrements.ContainsKey(wrappedSkill.Id))
                                {
                                    config.UpgradeIncrements[wrappedSkill.Id] = wrappedSkill.DefaultIncrement;
                                }
                                Console.WriteLine($"[CS2 RPG] Loaded skill: {wrappedSkill.DisplayName} (ID: {wrappedSkill.Id}) [Max Lvl: {wrappedSkill.DefaultMaxLevel}, Inc: {wrappedSkill.DefaultIncrement}]");
                                SkillLogger.Log($"SUCCESS: Loaded skill module -> DisplayName: {wrappedSkill.DisplayName}, ID: {wrappedSkill.Id}, DefaultMax: {wrappedSkill.DefaultMaxLevel}");
                            }
                        }
                        catch (Exception ex)
                        {
                            SkillLogger.Log($"ERROR: Failed to instantiate type {type.FullName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException?.Message ?? ex.Message;
                SkillLogger.Log($"ERROR: Failed to load skill DLL {Path.GetFileName(dllPath)}: {errorMsg}");
            }
        }

        SkillLogger.Log($"Total active skill modules loaded: {LoadedSkills.Count}");
    }
}

internal class ReflectionSkillWrapper : IRpgSkill
{
    private readonly dynamic _instance;
    private readonly Type _type;

    public string Id => (string)_type.GetProperty("Id")!.GetValue((object)_instance)!;
    public string DisplayName => (string)_type.GetProperty("DisplayName")!.GetValue((object)_instance)!;

    // Dynamically retrieve Description with a safe fallback if missing
    public string Description =>
        _type.GetProperty("Description") != null
            ? (string)_type.GetProperty("Description")!.GetValue((object)_instance)!
            : "No description provided.";

    public int DefaultMaxLevel => (int)_type.GetProperty("DefaultMaxLevel")!.GetValue((object)_instance)!;
    public double DefaultIncrement =>
        _type.GetProperty("DefaultIncrement") != null
            ? Convert.ToDouble(_type.GetProperty("DefaultIncrement")!.GetValue((object)_instance))
            : 5.0;

    public ReflectionSkillWrapper(object instance)
    {
        _instance = instance;
        _type = instance.GetType();
    }

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
        try
        {
            _instance.OnPlayerSpawn(playerSlot, level, upgradeIncrement);
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnPlayerSpawn failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement)
    {
        try
        {
            _instance.OnPlayerHurt(attackerSlot, victimSlot, damage, attackerLevel, victimLevel, upgradeIncrement);
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnPlayerHurt failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
        try
        {
            _instance.OnPeriodicTick(playerSlot, level, upgradeIncrement);
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnPeriodicTick failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public void OnPlayerJump(int playerSlot, int level, bool isGrounded, double upgradeIncrement)
    {
        try
        {
            var method = _type.GetMethod(nameof(OnPlayerJump), new[]
            {
                typeof(int),
                typeof(int),
                typeof(bool),
                typeof(double)
            });
            method?.Invoke((object)_instance, new object[] { playerSlot, level, isGrounded, upgradeIncrement });
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnPlayerJump failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public void OnPlayerJump(CCSPlayerController player, int level, bool isGrounded, double upgradeIncrement)
    {
        try
        {
            var method = _type.GetMethod(nameof(OnPlayerJump), new[]
            {
                typeof(CCSPlayerController),
                typeof(int),
                typeof(bool),
                typeof(double)
            });

            if (method != null)
            {
                method.Invoke((object)_instance, new object[] { player, level, isGrounded, upgradeIncrement });
                return;
            }

            OnPlayerJump(player.Slot, level, isGrounded, upgradeIncrement);
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnPlayerJump failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public void OnPlayerGroundState(int playerSlot, bool isGrounded)
    {
        try
        {
            var method = _type.GetMethod(nameof(OnPlayerGroundState));
            method?.Invoke((object)_instance, new object[] { playerSlot, isGrounded });
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnPlayerGroundState failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public void OnPlayerDeath(int playerSlot, int level, double upgradeIncrement)
    {
        try
        {
            _type.GetMethod(nameof(OnPlayerDeath))?.Invoke(
                (object)_instance,
                new object[] { playerSlot, level, upgradeIncrement });
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnPlayerDeath failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public int GetObjectiveXpBonus(int level, double upgradeIncrement)
    {
        try
        {
            var method = _type.GetMethod(nameof(GetObjectiveXpBonus));
            return method == null ? 0 : Convert.ToInt32(method.Invoke((object)_instance, new object[] { level, upgradeIncrement }));
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] GetObjectiveXpBonus failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
            return 0;
        }
    }

    public double GetBlindDurationMultiplier(int level, double upgradeIncrement)
    {
        try
        {
            var method = _type.GetMethod(nameof(GetBlindDurationMultiplier));
            return method == null ? 1.0 : Convert.ToDouble(method.Invoke((object)_instance, new object[] { level, upgradeIncrement }));
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] GetBlindDurationMultiplier failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
            return 1.0;
        }
    }

    public void OnRoundStart()
    {
        try
        {
            _type.GetMethod(nameof(OnRoundStart))?.Invoke((object)_instance, null);
        }
        catch (Exception ex)
        {
            SkillLogger.Log($"[EXECUTION ERROR] OnRoundStart failed for {Id}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}