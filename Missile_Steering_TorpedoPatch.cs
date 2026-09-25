using HarmonyLib;

namespace Torpedo
{
    [HarmonyPatch(typeof(Missile), "Steering")]
    internal static class Missile_Steering_TorpedoPatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (!TorpedoCombatRules.IsTorpedo(__instance)) return true;
            if (TorpedoPhysics.IsUnderWater(__instance)) return true;

            // In the air: during initial 0.35s tube exit, suppress aggressive donor steering.
            // Afterwards, let steering guide towards the water entry aimpoint with clamped angular velocity.
            TorpedoRuntimeController controller = __instance.GetComponent<TorpedoRuntimeController>();
            if (controller != null && controller.ElapsedLifetime < 0.35f)
                return false;

            return true;
        }
    }
}
