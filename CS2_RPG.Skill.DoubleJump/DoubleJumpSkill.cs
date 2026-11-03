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
        public bool FirstJumpSeen { get; set; }
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

    public void OnPlayerGroundState(int playerSlot, bool isGrounded)
    {
        if (!PlayerJumpStates.TryGetValue(playerSlot, out var state))
        {
            state = new JumpState();
            PlayerJumpStates[playerSlot] = state;
        }

        if (isGrounded)
        {
            state.FirstJumpSeen = false;
            state.UsedExtraJump = false;
        }
    }

    public void OnPlayerJump(int playerSlot, int level, bool isGrounded, double upgradeIncrement)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid) return;

        OnPlayerJump(player, level, isGrounded, upgradeIncrement);
    }

    public void OnPlayerJump(CCSPlayerController player, int level, bool isGrounded, double upgradeIncrement)
    {
        int playerSlot = player.Slot;
        if (!player.IsValid || !player.PawnIsAlive)
            return;

        if (!PlayerJumpStates.TryGetValue(playerSlot, out var state))
        {
            state = new JumpState();
            PlayerJumpStates[playerSlot] = state;
        }

        if (isGrounded)
        {
            state.FirstJumpSeen = true;
            state.UsedExtraJump = false;
            return;
        }

        if (!state.FirstJumpSeen)
        {
            state.FirstJumpSeen = true;
            return;
        }

        if (state.UsedExtraJump)
            return;

        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid)
            return;

        var velocity = pawn.Velocity;
        if (velocity == null)
            return;

        double increment = upgradeIncrement > 0 ? upgradeIncrement : DefaultIncrement;
        float bonusVelocity = (float)Math.Clamp(level * increment, 0, MaxBonusVelocity);
        float targetVelocity = BaseJumpVelocity + bonusVelocity;
        velocity.Z = targetVelocity;
        var absoluteVelocity = pawn.AbsVelocity;
        absoluteVelocity.Z = targetVelocity;
        state.UsedExtraJump = true;

        ulong steamId = player.SteamID;
        Server.NextFrame(() =>
        {
            var currentPlayer = Utilities.GetPlayerFromSlot(playerSlot);
            if (currentPlayer == null || !currentPlayer.IsValid || !currentPlayer.PawnIsAlive ||
                currentPlayer.SteamID != steamId ||
                !PlayerJumpStates.TryGetValue(playerSlot, out var currentState) ||
                !ReferenceEquals(currentState, state))
                return;

            var currentPawn = currentPlayer.PlayerPawn?.Value;
            if (currentPawn == null || !currentPawn.IsValid) return;

            var currentVelocity = currentPawn.Velocity;
            if (currentVelocity == null) return;

            if (currentVelocity.Z < targetVelocity)
                currentVelocity.Z = targetVelocity;
            var currentAbsoluteVelocity = currentPawn.AbsVelocity;
            if (currentAbsoluteVelocity.Z < targetVelocity)
                currentAbsoluteVelocity.Z = targetVelocity;
        });
    }
}
