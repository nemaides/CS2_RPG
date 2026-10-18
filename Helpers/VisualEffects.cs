using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2_RPG.Helpers;

public static class VisualEffects
{

    public static void SpawnWorldParticle(
        Vector position,
        string effectName = "particles/weapons/csgo_fx_gameplay/bullet_impact_world.vpcf",
        float destroyDelay = 1.5f,
        Action<Action, float>? scheduleTimer = null)
    {

        var particle = Utilities.CreateEntityByName<CBaseEntity>("info_particle_system");
        if (particle == null || !particle.IsValid) return;


        particle.DispatchSpawn();


        particle.Teleport(position, QAngle.Zero, Vector.Zero);
        particle.AcceptInput("Start");

        Action cleanupAction = () =>
        {
            if (particle != null && particle.IsValid)
            {
                particle.AcceptInput("Stop");
                particle.Remove();
            }
        };

        if (scheduleTimer != null)
        {
            scheduleTimer(cleanupAction, destroyDelay);
        }
        else
        {
            Server.NextFrame(cleanupAction);
        }
    }

    public static void SpawnGroundAura(
        Vector position,
        string effectName = "particles/inferno_fx/inferno_extinguish_ring.vpcf",
        float destroyDelay = 2.0f,
        Action<Action, float>? scheduleTimer = null)
    {
        var particle = Utilities.CreateEntityByName<CBaseEntity>("info_particle_system");
        if (particle == null || !particle.IsValid) return;

        particle.DispatchSpawn();

        var groundPos = new Vector(position.X, position.Y, position.Z - 2.0f);
        particle.Teleport(groundPos, QAngle.Zero, Vector.Zero);
        particle.AcceptInput("Start");

        Action cleanupAction = () =>
        {
            if (particle != null && particle.IsValid)
            {
                particle.AcceptInput("Stop");
                particle.Remove();
            }
        };

        if (scheduleTimer != null)
        {
            scheduleTimer(cleanupAction, destroyDelay);
        }
        else
        {
            Server.NextFrame(cleanupAction);
        }
    }
}