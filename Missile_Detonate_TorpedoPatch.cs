using HarmonyLib;
using UnityEngine;

namespace Torpedo
{
    [HarmonyPatch(typeof(Missile), "Detonate")]
    public static class Missile_Detonate_TorpedoPatch
    {
        public static bool Prefix(Missile __instance, Vector3 normal, bool hitArmor, bool hitTerrain)
        {
            if (!TorpedoCombatRules.IsTorpedo(__instance)) return true;

            // Allow detonation on authorized lethal gun kill after neutralizing warhead and motion
            if (TorpedoCombatRules.IsAuthorizedKill(__instance) || Missile_TakeDamage_TorpedoPatch.InProgress.Contains(__instance))
            {
                TorpedoCombatRules.NeutralizeDestroyedTorpedo(__instance);
                return true;
            }

            // Block detonation if torpedo is already disabled or killed
            if (__instance.disabled)
            {
                TorpedoCombatRules.NeutralizeDestroyedTorpedo(__instance);
                return false;
            }

            // Allow detonation on direct impact with armor/target
            if (hitArmor)
            {
                TorpedoCombatRules.ActiveTorpedoes.Remove(__instance);
                TorpedoWake.RemoveWake(__instance);
                return true;
            }

            // Allow miss self-destruction after swimming 100 meters straight
            TorpedoRuntimeController controller = __instance.GetComponent<TorpedoRuntimeController>();
            if (controller != null && controller.IsMissDetonating)
            {
                TorpedoCombatRules.ActiveTorpedoes.Remove(__instance);
                TorpedoWake.RemoveWake(__instance);
                return true;
            }

            // Allow detonation if hitting actual solid seabed terrain
            if (hitTerrain && (!TorpedoPhysics.IsOverWater(__instance) || __instance.GlobalPosition().y <= -1f))
            {
                TorpedoCombatRules.ActiveTorpedoes.Remove(__instance);
                TorpedoWake.RemoveWake(__instance);
                return true;
            }

            // Prevent unwanted self-destruction from seeker timeouts, proximity near-misses, or low speed
            return false;
        }
    }
}
