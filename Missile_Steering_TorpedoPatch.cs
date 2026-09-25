using HarmonyLib;

namespace Torpedo
{
    [HarmonyPatch(typeof(Missile), "Steering")]
    internal static class Missile_Steering_TorpedoPatch
    {
        private static bool Prefix(Missile __instance)
        {
            // VLS missiles launch vertically. The donor guidance tries to swing
            // immediately toward a sea-level target and spins the torpedo in air.
            // Normal steering resumes as soon as it enters the water.
            return !TorpedoCombatRules.IsTorpedo(__instance) ||
                   TorpedoPhysics.IsUnderWater(__instance);
        }
    }
}
