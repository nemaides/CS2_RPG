using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2_RPG.API;

namespace CS2_RPG.Skill.DoubleJump;

public class DoubleJumpSkill : IRpgSkill
{
    private const float BaseJumpVelocity = 300.0f;
    private const float MaxBonusVelocity = 60.0f;
    private static readonly Dictionary<int, JumpState> PlayerJumpStates = new();

    private sealed class JumpState
    {
        public bool JumpedFromGround { get; set; }
        public bool UsedExtraJump { get; set; }
    }

    public string Id => "double_jump";
    public string DisplayName => "Double Jump";
    public string Description => "Perform one extra jump while airborne after jumping from the ground.";
    public int DefaultMaxLevel => 5;
    public double DefaultIncrement => 10.0;

    public void OnPlayerSpawn(int playerSlot, int level, double upgradeIncrement)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid) return;

        PlayerJumpStates[playerSlot] = new JumpState();
    }

    public void OnPlayerHurt(int attackerSlot, int victimSlot, int damage, int attackerLevel, int victimLevel, double upgradeIncrement) { }

    public void OnPeriodicTick(int playerSlot, int level, double upgradeIncrement) { }

    public void OnPlayerJump(int playerSlot, int level, bool isGrounded, double upgradeIncrement)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        if (!PlayerJumpStates.TryGetValue(playerSlot, out var state))
        {
            state = new JumpState();
            PlayerJumpStates[playerSlot] = state;
        }

        if (isGrounded)
        {
            state.JumpedFromGround = true;
            state.UsedExtraJump = false;
            return;
        }

        if (!state.JumpedFromGround || state.UsedExtraJump) return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid || pawn.AbsVelocity == null) return;

        double increment = upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement;
        float bonusVelocity = (float)Math.Clamp(level * increment, 0, MaxBonusVelocity);
        pawn.AbsVelocity.Z = BaseJumpVelocity + bonusVelocity;
        state.UsedExtraJump = true;
    }
}
