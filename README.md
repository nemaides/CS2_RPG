# CS2 RPG 

**CS2 RPG** is a modular, high-performance RPG plugin framework for Counter-Strike 2 servers powered by **CounterStrikeSharp** and **.NET 10.0**. 

The framework features dynamic Assembly Load Context (ALC) skill loading, automated configuration synchronization, and a clean separation between the core plugin API and individual skill modules.

---

## Key Features

- **Modular Architecture:** Skills are built as standalone C# class libraries (`.dll` files) and dynamically discovered at runtime without modifying core plugin code.
- **ALC Safety:** Uses primitive parameter types across boundaries to ensure assembly isolation and memory safety.
- **Automatic Configuration Sync:** Missing skill defaults automatically populate into `rpg_config.json` upon initial discovery.
- **Extensible Hooks:** Seamlessly handle player events such as spawns, damage events, and background periodic ticks.

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
    string Id { get; }
    string DisplayName { get; }
    int DefaultMaxLevel { get; }
    double DefaultIncrement { get; }

    void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement);
    void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement);
    void OnPeriodicTick(int playerSlot, int level);
}
```

---

### 3. Skill Template Example
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

    public void OnPeriodicTick(int playerSlot, int level)
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
            int healAmount = (int)Math.Round(level * DefaultIncrement);
            pawn.Health = Math.Min(pawn.Health + healAmount, maxHp);

            // Synchronize updated health property over the network graph
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }
    }
}
```

---

### 4. Key Rules & Best Practices

| Mechanism | Recommendation / Rule |
| :--- | :--- |
| **Player Lookup** | Always look up players using `Utilities.GetPlayerFromSlot(playerSlot)`. Do not store direct player controller references across ticks. |
| **Pawn Validation** | Check `if (pawn == null || !pawn.IsValid || pawn.Health <= 0)` before accessing or modifying pawn attributes. |
| **Network State Sync** | Call `Utilities.SetStateChanged(entity, className, propertyName)` whenever modifying entity properties (such as `m_iHealth`, `m_flVelocityModifier`, or `m_iClip1`). |
| **Config Synchronization** | `DefaultMaxLevel` and `DefaultIncrement` automatically populate missing entries into `rpg_config.json` upon initial discovery. |

---

### 5. Deployment

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

## ToDo
I plan on adding a /rpghelp command, that explains what the skill does, of which the skill developer must state when developing the skill
Maybe even extend so you do /rpghelp Speed (for the speed skill) so it only shows that specific skill help text.

## License

This project is open-source and available under the terms specified in the repository.
