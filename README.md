<sup>Disclaimer:</sup>

<sup>This README.md was made using AI, i cannot make things pretty even if my life depended on it.</sup>

<sup>Regarding the code, AI was used to help debug some parts and as a helper tool, the code is NOT fully AI generated</sup>

# CS2 RPG 

**CS2 RPG** is a modular, high-performance RPG plugin framework for Counter-Strike 2 servers powered by **CounterStrikeSharp** and **.NET 10.0**. 

The framework features dynamic Assembly Load Context (ALC) skill loading, automated configuration synchronization, and a clean separation between the core plugin API and individual skill modules.

---

## Key Features

- **Modular Architecture:** Skills are compiled as standalone C# class libraries (`.dll` files) and dynamically discovered at runtime without modifying core plugin code.
- **ALC & Boundary Safety:** Uses primitive parameter types across boundaries to ensure assembly isolation and memory safety.
- **Automatic Configuration Sync:** Missing skill defaults automatically populate into `rpg_config.json` upon initial discovery.
- **Dynamic Attributed Damage & Frags:** Helper actions like `RpgPlayerActions.KillPlayer()` allow custom skills (e.g., Poison Smoke) to register killfeed icons and increment scoreboard stats cleanly without triggering suicides or 0 HP ghosting state bugs.
- **Interactive Commands & Help:** Players can use `/rpg`, `/stats`, and `/rpghelp` (with targeted help queries such as `/rpghelp speed`) to view detailed skill descriptions.

---

### Commands Reference

| Command | Description |
| :--- | :--- |
| **/rpg, /rpg_menu, /rpg_upgrade** | Opens the main RPG upgrade menu. |
| **/stats, /rpg_stats** | Displays your current RPG stats and level progress. |
| **/rpghelp [skill]** | Lists all registered RPG skills or details a specific skill. |
| **/rpg_admin** | Displays available RPG admin commands. |

---

## Project Structure

```text
CS2_RPG/
CS2_RPG.API/          # Core shared interfaces and contracts (IRpgSkill)
Skills/               # External custom skill modules (e.g., Regen, Speed, etc.)
```

---

## Developer Guide: Creating a New Skill Module

This manual details how to create, build, and deploy a new skill DLL that integrates seamlessly into the core CS2_RPG framework via automatic dynamic registration.

### 1. Prerequisites & Project Setup
1. Ensure you have **.NET 8.0 SDK** installed.
2. Create a new C# Class Library project via CLI or Visual Studio:
   ```bash
   dotnet new classlib -n CS2_RPG.Skill.YourSkillName
   ```
3. Reference the core API assembly (`CS2_RPG.API.dll`) and CounterStrikeSharp dependencies:
   - `CounterStrikeSharp.API.dll`
   - `CS2_RPG.API.dll`

---

### 2. Implementing the `IRpgSkill` Interface
Every skill must implement the `IRpgSkill` interface. The interface relies strictly on primitive parameter types (`int`, `double`) to ensure ALC (Assembly Load Context) safety across DLL boundaries.

#### Interface Definition Reference (`CS2_RPG.API`)
```csharp
namespace CS2_RPG.API;

public interface IRpgSkill
{
    /// <summary>Unique identifier for config/database (e.g., "speed", "poison_smoke")</summary>
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
}
```

---

### 3. Handling Kills & Dynamic Attribution
If your custom skill deals custom/periodic damage (e.g., Poison Smoke, Fire, or Flashbang damage) and needs to kill the victim with proper attribution:

```csharp
using CS2_RPG.API;

// Safely kills the victim, credits the attacker, increments scoreboard kills,
// and broadcasts the custom weapon icon in the killfeed.
RpgPlayerActions.KillPlayer(attackerSlot, victimSlot, "smokegrenade");
```

---

### 4. Skill Template Example
Here is a ready-to-use template for a new skill (e.g., a **Health Regeneration Skill**):

```csharp
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.Regen;

public class RegenSkill : IRpgSkill
{
    // Unique identifier used in config key mapping & player data
    public string Id => "regen";

    // Name displayed in logging and UI
    public string DisplayName => "Health Regeneration";

    // Description shown in /rpghelp and skill menus
    public string Description => "Slowly restores your health over time when injured.";

    // Initial default max level for rpg_config.json
    public int DefaultMaxLevel => 10;

    // Initial default scaling increment per level
    public double DefaultIncrement => 2.0; // Grants 2 HP per periodic tick per level

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
        // Executes when a player spawns. Leave empty if not needed.
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement)
    {
        // Executes when player damage occurs. Leave empty if not needed.
    }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement)
    {
        // Executes on the background timer (default every 3s)
        if (level <= 0 || playerSlot < 0) return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return;

        int maxHp = pawn.MaxHealth > 0 ? pawn.MaxHealth : 100;

        if (pawn.Health < maxHp)
        {
            int healAmount = (int)Math.Round(level * upgradeIncrement);
            pawn.Health = Math.Min(pawn.Health + healAmount, maxHp);

            // Synchronize updated health property over the network graph
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }
    }
}
```

---

### 5. Key Rules & Best Practices

| Mechanism | Recommendation / Rule |
| :--- | :--- |
| **Player Lookup** | Always look up players using `Utilities.GetPlayerFromSlot(playerSlot)`. Do not store direct player controller references across ticks. |
| **Pawn Validation** | Check `if (pawn == null || !pawn.IsValid || pawn.Health <= 0)` before accessing or modifying pawn attributes. |
| **Network State Sync** | Call `Utilities.SetStateChanged(entity, className, propertyName)` whenever modifying entity properties (such as `m_iHealth`, `m_flVelocityModifier`, or `m_iClip1`). |
| **Config Synchronization** | `DefaultMaxLevel` and `DefaultIncrement` automatically populate missing entries into `rpg_config.json` upon initial discovery. |

---

### 6. Deployment

1. **Build Solution:** Compile the skill project (`CS2_RPG.Skill.YourSkillName.dll`).
2. **Deploy DLL:** Copy the output `.dll` file directly into your server's Skills directory:
   ```text
   addons/counterstrikesharp/plugins/CS2_RPG/Skills/
   ```
3. **Reload Plugin:** Execute the following command in your server console:
   ```bash
   css_plugins reload CS2_RPG
   ```

The `SkillManager` will automatically discover the DLL, output registration details to the terminal, and synchronize its defaults into `rpg_config.json`.

---

## License

This project is open-source and available under the terms specified in the repository.
