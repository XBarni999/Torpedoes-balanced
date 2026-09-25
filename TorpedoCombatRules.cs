using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace Torpedo
{
    internal static class TorpedoCombatRules
    {
        internal const float DetectionRange = 7000f;
        private static readonly Dictionary<int, float> AuthorizedGunHits = new Dictionary<int, float>();
        private static readonly HashSet<int> AuthorizedKills = new HashSet<int>();
        internal static readonly HashSet<Missile> ActiveTorpedoes = new HashSet<Missile>();

        private static readonly Regex SmallCaliberPattern = new Regex(@"(?<!\d)(?:12|14|20|23|25|27|30)(?:[\.,]\d+)?\s*mm\b", RegexOptions.IgnoreCase);
        private static readonly Regex SmallArmsKeywordPattern = new Regex(@"\b(rotary|ciws|vulcan|gatling|autocannon|chain\s*gun|machine\s*gun|pdw|rifle)\b", RegexOptions.IgnoreCase);
        private static readonly Regex CaliberPattern = new Regex(@"(?<!\d)(\d+(?:[\.,]\d+)?)\s*mm", RegexOptions.IgnoreCase);
        private static readonly Regex BareCaliberPattern = new Regex(@"(?<!\d)(\d{2,3})(?!\d)");
        private static readonly Regex InchCaliberPattern = new Regex("(?<!\\d)(\\d+(?:[\\.,]\\d+)?)\\s*(?:in|inch|\")", RegexOptions.IgnoreCase);
        private static readonly Regex HeavyGunKeywordPattern = new Regex(@"\b(artillery|battery|naval|deck|howitzer|heavy)\b", RegexOptions.IgnoreCase);

        internal static bool IsTorpedo(Unit unit)
        {
            Missile missile = unit as Missile;
            if (missile == null) return false;
            if (missile.GetComponent<TorpedoIdentity>() != null) return true;

            // Mirage can instantiate a network object without preserving third-party
            // MonoBehaviours added to the source prefab. The cloned definition is unique
            // to this mod and is therefore the authoritative runtime fallback.
            return missile.definition != null &&
                   TorpedoMounts_Patch.HoverAltitudeByName.ContainsKey(missile.definition.jsonKey);
        }

        internal static bool TryGetHoverAltitude(Missile missile, out float altitude)
        {
            altitude = 0f;
            return IsTorpedo(missile) && missile.definition != null &&
                   TorpedoMounts_Patch.HoverAltitudeByName.TryGetValue(missile.definition.jsonKey, out altitude);
        }

        internal static void RegisterDirectGunHit(Unit target, WeaponInfo weaponInfo)
        {
            if (!IsTorpedo(target) || !IsGunOver30Mm(weaponInfo)) return;
            AuthorizedGunHits[target.GetInstanceID()] = Time.unscaledTime + 3f;
        }

        internal static bool IsAuthorizedGunHit(Missile missile)
        {
            if (missile == null) return false;
            int id = missile.GetInstanceID();
            if (!AuthorizedGunHits.TryGetValue(id, out float expires)) return false;
            return Time.unscaledTime <= expires;
        }

        internal static bool ConsumeAuthorizedGunHit(Missile missile) => IsAuthorizedGunHit(missile);

        internal static void MarkAuthorizedKill(Missile missile)
        {
            if (missile != null) AuthorizedKills.Add(missile.GetInstanceID());
        }

        internal static bool IsAuthorizedKill(Missile missile)
        {
            if (missile == null) return false;
            return AuthorizedKills.Contains(missile.GetInstanceID());
        }

        internal static void NeutralizeDestroyedTorpedo(Missile missile)
        {
            if (missile == null) return;
            MarkAuthorizedKill(missile);
            ActiveTorpedoes.Remove(missile);
            TorpedoWake.RemoveWake(missile);

            // Disarm warhead and clear blast yield so neutralized torpedo cannot damage ships
            try
            {
                Traverse.Create(missile).Field("blastYield").SetValue(0f);
                object warhead = Traverse.Create(missile).Field("warhead").GetValue();
                if (warhead != null)
                {
                    Traverse.Create(warhead).Field("Armed").SetValue(false);
                    Traverse.Create(warhead).Field("detonated").SetValue(true);
                }
            }
            catch { }

            // Halt all motion instantly
            try
            {
                if (missile.rb != null)
                {
                    missile.rb.velocity = Vector3.zero;
                    missile.rb.angularVelocity = Vector3.zero;
                    missile.rb.isKinematic = true;
                }
            }
            catch { }

            // Disable colliders and intangibility to eliminate collision ghosting
            try
            {
                missile.SetTangible(false);
                foreach (Collider col in missile.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }
            catch { }

            // Hide visual renderers immediately
            try
            {
                foreach (MeshRenderer r in missile.GetComponentsInChildren<MeshRenderer>())
                {
                    r.enabled = false;
                }
            }
            catch { }

            // Cleanly destroy the object immediately so it cannot remain as a ghost
            try
            {
                UnityEngine.Object.Destroy(missile.gameObject, 0.05f);
            }
            catch { }
        }

        internal static bool IsGunOver30Mm(WeaponInfo info)
        {
            if (info == null || !info.gun) return false;

            string text = (info.weaponName ?? string.Empty) + " " +
                          (info.shortName ?? string.Empty) + " " +
                          (info.description ?? string.Empty);

            // 1. Immediately reject small-caliber guns and CIWS / rotary / autocannon keywords
            if (SmallCaliberPattern.IsMatch(text) || SmallArmsKeywordPattern.IsMatch(text))
                return false;

            // 2. Explicit millimeter caliber check: must be strictly > 35mm (e.g. 76mm, 128mm, 406mm)
            Match match = CaliberPattern.Match(text);
            if (match.Success && float.TryParse(match.Groups[1].Value.Replace(',', '.'),
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out float caliber))
            {
                return caliber > 35f;
            }

            // 3. Explicit inch caliber check (e.g. 5 in, 16 inch)
            match = InchCaliberPattern.Match(text);
            if (match.Success && float.TryParse(match.Groups[1].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out caliber))
            {
                return caliber * 25.4f > 35f;
            }

            // 4. Bare caliber digits (e.g. "406", "128")
            match = BareCaliberPattern.Match((info.weaponName ?? string.Empty) + " " + (info.shortName ?? string.Empty));
            if (match.Success && float.TryParse(match.Groups[1].Value,
                       System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture, out caliber))
            {
                return caliber > 35f;
            }

            // 5. Heavy naval gun ballistic threshold: heavy naval shells have high damage
            if (info.pierceDamage >= 30f || info.blastDamage >= 40f)
                return true;

            // 6. Heavy naval artillery keywords
            if (HeavyGunKeywordPattern.IsMatch(text))
                return true;

            return false;
        }

        internal static bool IsWithinTorpedoLaunchRange(WeaponInfo info, Unit owner, Unit target, GlobalPosition aimpoint)
        {
            if (!TorpedoMounts_Patch.IsBalancedTorpedoInfo(info) || owner == null) return true;
            float maxRange = info.targetRequirements.maxRange;
            if (maxRange <= 0f) return false;
            if (target != null)
                return FastMath.InRange(owner.GlobalPosition(), target.GlobalPosition(), maxRange);
            return FastMath.InRange(owner.GlobalPosition(), aimpoint, maxRange);
        }
    }

    // Let ship sensors report submerged torpedoes through the normal detector event path.
    // DetectTarget updates HQ tracking before TargetDetector raises its end-of-scan event,
    // so turrets never receive an orphaned target ID.
    [HarmonyPatch(typeof(TargetDetector), "TargetSearch")]
    internal static class TorpedoShipDetectionPatch
    {
        private static void Postfix(TargetDetector __instance)
        {
            AddNearbyTorpedoes(__instance);
        }

        internal static void AddNearbyTorpedoes(TargetDetector __instance)
        {
            Unit observer = __instance != null ? __instance.GetAttachedUnit() : null;
            if (!(observer is Ship) || observer.disabled || observer.NetworkHQ == null) return;

            TorpedoCombatRules.ActiveTorpedoes.RemoveWhere(m => m == null || m.disabled);
            foreach (Missile torpedo in TorpedoCombatRules.ActiveTorpedoes)
            {
                if (torpedo == null || torpedo.disabled || torpedo.NetworkHQ == null ||
                    torpedo.NetworkHQ == observer.NetworkHQ ||
                    __instance.detectedTargets.Contains(torpedo)) continue;
                if (!FastMath.InRange(observer.GlobalPosition(), torpedo.GlobalPosition(), TorpedoCombatRules.DetectionRange))
                    continue;
                __instance.DetectTarget(torpedo);
            }
        }
    }

    // Radar overrides TargetSearch, so patch the override as well as the base visual detector.
    [HarmonyPatch(typeof(Radar), "TargetSearch")]
    internal static class TorpedoShipRadarDetectionPatch
    {
        private static void Postfix(Radar __instance)
        {
            TorpedoShipDetectionPatch.AddNearbyTorpedoes(__instance);
        }
    }

    [HarmonyPatch(typeof(TargetDetector), "DetectTarget")]
    internal static class TorpedoDetectionRangePatch
    {
        private static bool Prefix(TargetDetector __instance, Unit target)
        {
            if (!TorpedoCombatRules.IsTorpedo(target)) return true;
            Unit observer = __instance != null ? __instance.GetAttachedUnit() : null;
            return observer is Ship && observer.NetworkHQ != target.NetworkHQ &&
                   FastMath.InRange(observer.GlobalPosition(), target.GlobalPosition(), TorpedoCombatRules.DetectionRange);
        }
    }

    // Torpedoes are valid targets for direct-fire naval guns (>35mm).
    // Secondary AA guns and large naval guns prioritize them if within range.
    [HarmonyPatch(typeof(Turret), "AssessTargetPriority")]
    internal static class TorpedoTurretPriorityPatch
    {
        private static readonly AccessTools.FieldRef<Turret, Unit> AttachedUnit =
            AccessTools.FieldRefAccess<Turret, Unit>("attachedUnit");
        private static readonly AccessTools.FieldRef<Turret, WeaponStation[]> Stations =
            AccessTools.FieldRefAccess<Turret, WeaponStation[]>("weaponStations");
        private static readonly AccessTools.FieldRef<Turret, Unit> Target =
            AccessTools.FieldRefAccess<Turret, Unit>("target");
        private static readonly AccessTools.FieldRef<Turret, WeaponStation> CurrentStation =
            AccessTools.FieldRefAccess<Turret, WeaponStation>("currentWeaponStation");

        private static bool Prefix(Turret __instance, Unit targetCandidate, ref float priorityThreshold)
        {
            if (!TorpedoCombatRules.IsTorpedo(targetCandidate)) return true;
            Unit owner = AttachedUnit(__instance);
            if (owner == null || owner.NetworkHQ == null || targetCandidate.disabled ||
                owner.NetworkHQ.GetTrackingData(targetCandidate.persistentID) == null) return false;

            float distance = Vector3.Distance(owner.transform.position, targetCandidate.transform.position);
            WeaponStation selected = null;
            foreach (WeaponStation station in Stations(__instance))
            {
                if (station == null || station.WeaponInfo == null || station.Ammo <= 0 ||
                    !TorpedoCombatRules.IsGunOver30Mm(station.WeaponInfo)) continue;
                TargetRequirements requirements = station.WeaponInfo.targetRequirements;
                // Soften minimum range for anti-torpedo defense so large naval guns can engage incoming threats
                float minRange = Mathf.Min(requirements.minRange, 200f);
                float maxRange = Mathf.Min(requirements.maxRange, TorpedoCombatRules.DetectionRange);
                if (distance < minRange || distance > maxRange)
                    continue;
                selected = station;
                if (!station.Reloading) break;
            }
            if (selected == null) return false;

            float priority = selected.Reloading ? 10000f : 100000f;
            if (priority > priorityThreshold)
            {
                Target(__instance) = targetCandidate;
                CurrentStation(__instance) = selected;
                priorityThreshold = priority;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(WeaponStation), "Fire")]
    internal static class TorpedoMissileFireGuardPatch
    {
        private static bool Prefix(WeaponStation __instance, Unit owner, Unit target)
        {
            if (__instance == null) return true;
            if (TorpedoCombatRules.IsTorpedo(target) && !TorpedoCombatRules.IsGunOver30Mm(__instance.WeaponInfo))
                return false;
            return TorpedoCombatRules.IsWithinTorpedoLaunchRange(
                __instance.WeaponInfo, owner,
                target, default(GlobalPosition));
        }
    }

    [HarmonyPatch(typeof(Gun), "Fire")]
    internal static class TorpedoGunFireGuardPatch
    {
        private static bool Prefix(Gun __instance, Unit target)
        {
            if (__instance == null) return true;
            if (TorpedoCombatRules.IsTorpedo(target) && !TorpedoCombatRules.IsGunOver30Mm(__instance.info))
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(WeaponStation), "LaunchMount")]
    internal static class TorpedoMountedMissileFireGuardPatch
    {
        private static bool Prefix(WeaponStation __instance, Unit owner, Unit target, GlobalPosition aimpoint)
        {
            if (__instance == null) return true;
            if (TorpedoCombatRules.IsTorpedo(target) && !TorpedoCombatRules.IsGunOver30Mm(__instance.WeaponInfo))
                return false;
            return TorpedoCombatRules.IsWithinTorpedoLaunchRange(__instance.WeaponInfo, owner, target, aimpoint);
        }
    }

    [HarmonyPatch(typeof(MountedMissile), "Fire")]
    internal static class TorpedoMountedMissileRangePatch
    {
        private static bool Prefix(MountedMissile __instance, Unit owner, Unit target, GlobalPosition aimpoint)
        {
            return __instance == null || TorpedoCombatRules.IsWithinTorpedoLaunchRange(__instance.info, owner, target, aimpoint);
        }
    }

    [HarmonyPatch(typeof(MissileLauncher), "Fire")]
    internal static class TorpedoMissileLauncherRangePatch
    {
        private static bool Prefix(MissileLauncher __instance, Unit owner, Unit target, GlobalPosition aimpoint)
        {
            return __instance == null || TorpedoCombatRules.IsWithinTorpedoLaunchRange(__instance.info, owner, target, aimpoint);
        }
    }

    [HarmonyPatch(typeof(WeaponStation), "RegisterWeapon")]
    internal static class TorpedoWeaponRegistrationPatch
    {
        private static readonly AccessTools.FieldRef<Weapon, WeaponInfo> WeaponInfoField =
            AccessTools.FieldRefAccess<Weapon, WeaponInfo>("info");

        private static void Prefix(Weapon weapon, WeaponMount weaponMount)
        {
            WeaponInfo balanced;
            if (weapon == null || weaponMount == null ||
                !TorpedoMounts_Patch.TryGetBalancedInfo(weaponMount.name, out balanced)) return;
            WeaponInfoField(weapon) = balanced;
            weaponMount.info = balanced;
        }
    }

    [HarmonyPatch(typeof(Unit), "RegisterHit")]
    internal static class TorpedoDirectGunHitPatch
    {
        private static bool Prefix(Unit hitUnit, WeaponInfo weaponInfo)
        {
            if (!TorpedoCombatRules.IsTorpedo(hitUnit)) return true;
            if (!TorpedoCombatRules.IsGunOver30Mm(weaponInfo)) return false;
            TorpedoCombatRules.RegisterDirectGunHit(hitUnit, weaponInfo);
            return true;
        }
    }

    [HarmonyPatch(typeof(Unit), "ReportKilled")]
    internal static class TorpedoReportKilledPatch
    {
        private static readonly HashSet<int> ReportedUnits = new HashSet<int>();

        internal static void Clear(int id) => ReportedUnits.Remove(id);

        private static bool Prefix(Unit __instance)
        {
            if (__instance is Missile missile && TorpedoCombatRules.IsTorpedo(missile))
            {
                int id = missile.GetInstanceID();
                if (ReportedUnits.Contains(id))
                {
                    return false; // Suppress duplicate kill messages
                }
                ReportedUnits.Add(id);
            }
            return true;
        }
    }
}
