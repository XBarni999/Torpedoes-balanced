using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Torpedo
{
    [HarmonyPatch(typeof(Missile), "TakeDamage")]
    public static class Missile_TakeDamage_TorpedoPatch
    {
        internal static readonly HashSet<Missile> InProgress = new HashSet<Missile>();
        private static readonly AccessTools.FieldRef<Missile, float> HitpointsRef =
            AccessTools.FieldRefAccess<Missile, float>("hitpoints");
        private static readonly AccessTools.FieldRef<Missile, ArmorProperties> ArmorPropertiesRef =
            AccessTools.FieldRefAccess<Missile, ArmorProperties>("armorProperties");

        [HarmonyPrefix]
        public static bool Prefix(Missile __instance, ref float pierceDamage, ref float blastDamage, ref float amountAffected, ref float fireDamage, ref float impactDamage, PersistentID dealerID, ref bool __state)
        {
            __state = false;
            if (!TorpedoCombatRules.IsTorpedo(__instance)) return true;

            // Reject damage if already destroyed, disabled, or neutralized
            if (__instance.disabled || TorpedoCombatRules.IsAuthorizedKill(__instance) || HitpointsRef(__instance) <= 0f)
                return false;

            // Only direct kinetic bullet hit (pierceDamage >= 15f) is allowed to harm torpedoes.
            // Proximity flak exploding above water, airburst blasts, and splashes deal 0 damage.
            if (pierceDamage < 15f)
                return false;

            if (!TorpedoCombatRules.IsAuthorizedGunHit(__instance))
                return false;

            // Suppress all blast, fire, and impact damage
            blastDamage = 0f;
            fireDamage = 0f;
            impactDamage = 0f;

            // Scale down bullet damage so it requires multiple direct hits to destroy
            pierceDamage *= 0.5f;

            // If this direct kinetic hit is lethal, mark as authorized kill and neutralize immediately
            ArmorProperties armor = ArmorPropertiesRef(__instance);
            float effectivePierce = Mathf.Max(pierceDamage - armor.pierceArmor, 0f) / Mathf.Max(armor.pierceTolerance, 0.1f);
            if (HitpointsRef(__instance) - effectivePierce <= 0f)
            {
                TorpedoCombatRules.NeutralizeDestroyedTorpedo(__instance);
            }

            InProgress.Add(__instance);
            __state = true;
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Missile __instance, bool __state)
        {
            if (__state) InProgress.Remove(__instance);
        }
    }

    [HarmonyPatch(typeof(UnitPart), "TakeDamage")]
    public static class UnitPart_TakeDamage_TorpedoPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(UnitPart __instance, float pierceDamage, float blastDamage, float amountAffected, float fireDamage, float impactDamage, PersistentID dealerID)
        {
            if (__instance == null || __instance.parentUnit == null) return true;
            if (__instance.parentUnit is Missile missile && TorpedoCombatRules.IsTorpedo(missile))
            {
                // Route all damage through the parent Missile so combat rules and kill handling apply uniformly
                if (missile.disabled || TorpedoCombatRules.IsAuthorizedKill(missile)) return false;
                missile.TakeDamage(pierceDamage, blastDamage, amountAffected, fireDamage, impactDamage, dealerID);
                return false; // Prevent UnitPart from applying damage independently or calling duplicate ReportKilled
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Missile), "TakeShockwave")]
    public static class Missile_TakeShockwave_TorpedoPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Missile __instance)
        {
            // Submerged torpedoes are immune to near-miss shockwaves from air/water surface flak
            if (TorpedoCombatRules.IsTorpedo(__instance))
            {
                return false;
            }
            return true;
        }
    }
}
