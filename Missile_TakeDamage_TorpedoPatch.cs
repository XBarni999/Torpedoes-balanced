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

            // Only direct kinetic bullet hit (pierceDamage >= 15f) is allowed to harm torpedoes.
            // Proximity flak exploding above water, airburst blasts, and splashes deal 0 damage.
            if (pierceDamage < 15f)
                return false;

            if (!TorpedoCombatRules.ConsumeAuthorizedGunHit(__instance))
                return false;

            // Suppress all blast, fire, and impact damage
            blastDamage = 0f;
            fireDamage = 0f;
            impactDamage = 0f;

            // Scale down bullet damage so it requires multiple direct hits to destroy
            pierceDamage *= 0.5f;

            // If this direct kinetic hit is lethal, mark as authorized kill so Detonate properly stops and destroys it
            ArmorProperties armor = ArmorPropertiesRef(__instance);
            float effectivePierce = Mathf.Max(pierceDamage - armor.pierceArmor, 0f) / Mathf.Max(armor.pierceTolerance, 0.1f);
            if (HitpointsRef(__instance) - effectivePierce <= 0f)
            {
                TorpedoCombatRules.MarkAuthorizedKill(__instance);
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
