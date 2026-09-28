using HarmonyLib;

namespace Torpedo
{
    [HarmonyPatch(typeof(Missile), "Steering")]
    internal static class Missile_Steering_TorpedoPatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (!TorpedoCombatRules.IsTorpedo(__instance)) return true;
            // The donor missile steers toward a sea-level aimpoint while the booster
            // still pushes along an angled Lemon launcher. Those competing rotations
            // make it tumble. Keep its launch attitude until water entry.
            return TorpedoPhysics.IsUnderWater(__instance);
        }
    }
}
