using HarmonyLib;
using UnityEngine;
namespace Torpedo
{
    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), "TerminalMode")]
    public static class OpticalSeekerCruiseMissile_TerminalMode_TorpedoPatch
    {
        private static readonly AccessTools.FieldRef<MissileSeeker, Missile> missileRef =
            AccessTools.FieldRefAccess<MissileSeeker, Missile>("missile");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> targetUnitRef =
            AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");
        public static bool Prefix(OpticalSeekerCruiseMissile __instance)
        {
            Missile missile = missileRef(__instance);
            if (!TorpedoCombatRules.IsTorpedo(missile)) return true;
            TorpedoRuntimeController controller = missile.GetComponent<TorpedoRuntimeController>();
            if (controller != null && controller.HasMissed)
            {
                GlobalPosition aim = missile.GlobalPosition() + controller.MissForward * 10000f;
                aim.y = TorpedoCombatRules.TryGetHoverAltitude(missile, out float alt) ? alt : -1f;
                missile.SetAimpoint(aim, Vector3.zero);
                return false;
            }
            if (targetUnitRef(__instance) != null) return true; 
            GlobalPosition aimPoint = missile.GlobalPosition() + missile.transform.forward * 100000f;
            missile.SetAimpoint(aimPoint, Vector3.zero);
            return false;
        }
    }
}
