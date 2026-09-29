using System;
using HarmonyLib;
using UnityEngine;

namespace Torpedo
{
    // This component exists only on cloned torpedo prefabs. Keeping launch cutoff,
    // underwater propulsion and wake here avoids patching Missile.FixedUpdate or
    // MotorThrust for every vanilla projectile in the game.
    internal sealed class TorpedoRuntimeController : MonoBehaviour
    {
        private const float BoosterDuration = 0.8f;
        private const float MissDetonateDistance = 100f;

        private static readonly AccessTools.FieldRef<Missile, GlobalPosition> AimPointRef =
            AccessTools.FieldRefAccess<Missile, GlobalPosition>("aimPoint");
        private static readonly AccessTools.FieldRef<Missile, Unit> TargetRef =
            AccessTools.FieldRefAccess<Missile, Unit>("target");
        private static readonly AccessTools.FieldRef<Missile, MissileSeeker> SeekerRef =
            AccessTools.FieldRefAccess<Missile, MissileSeeker>("seeker");
        private static readonly AccessTools.FieldRef<MissileSeeker, Unit> SeekerTargetUnitRef =
            AccessTools.FieldRefAccess<MissileSeeker, Unit>("targetUnit");

        private Missile missile;
        private bool launchMotorStopped;
        private bool enteredWater;
        private float lastPhysicsTick = float.NegativeInfinity;
        private float elapsedLifetime;
        private float boosterBurnTime;

        // Miss tracking
        internal bool HasMissed { get; private set; }
        internal Vector3 MissForward { get; private set; }
        internal bool IsMissDetonating { get; private set; }
        private float distanceTraveledSinceMiss;
        private float closestTargetDistance = float.MaxValue;

        internal float ElapsedLifetime => elapsedLifetime;

        private void Awake()
        {
            missile = GetComponent<Missile>();
        }

        private void FixedUpdate()
        {
            Tick();
        }

        internal void Tick()
        {
            if (missile == null) missile = GetComponent<Missile>();
            if (missile == null || missile.disabled) return;
            if (Mathf.Approximately(lastPhysicsTick, Time.fixedTime)) return;
            lastPhysicsTick = Time.fixedTime;
            elapsedLifetime += Time.fixedDeltaTime;

            bool underwater = TorpedoPhysics.IsUnderWater(missile);
            VLSBooster booster = missile.GetComponentInChildren<VLSBooster>();
            bool boosterAttached = booster != null && missile.boosterIsAttached;
            bool isAirDrop = (missile.owner is Aircraft);

            if (boosterAttached)
            {
                try
                {
                    if (Traverse.Create(booster).Field("activated").GetValue<bool>())
                        boosterBurnTime += Time.fixedDeltaTime;
                }
                catch { boosterBurnTime += Time.fixedDeltaTime; }
            }

            if (underwater && !enteredWater)
            {
                enteredWater = true;
                TorpedoPhysics.OnWaterEntry(missile, hoverAltitude: GetHoverAltitude());
            }

            if (!launchMotorStopped && (underwater ||
                (boosterAttached ? boosterBurnTime >= BoosterDuration : elapsedLifetime >= BoosterDuration)))
            {
                StopLaunchMotor();
            }

            if (missile.rb != null && !missile.rb.isKinematic)
            {
                // Vanilla missiles disable gravity. After the launch impulse (or immediately on air drop),
                // enable gravity so the torpedo arcs and dives into the sea.
                missile.rb.useGravity = (launchMotorStopped || isAirDrop) && !underwater;

                if (!underwater)
                {
                    if (missile.LocalSim)
                    {
                        if (!isAirDrop)
                        {
                            GuideBoosterTowardsTarget();
                        }
                        else
                        {
                            missile.rb.angularVelocity = Vector3.zero;
                        }
                    }
                }
            }

            if (!TorpedoCombatRules.TryGetHoverAltitude(missile, out float hoverAltitude)) return;

            if (underwater)
            {
                // Check for miss / overshoot when tracking a target
                if (!HasMissed)
                {
                    CheckTargetMiss();
                }
                else
                {
                    float currentSpeed = missile.rb != null ? missile.rb.velocity.magnitude : 0f;
                    distanceTraveledSinceMiss += currentSpeed * Time.fixedDeltaTime;
                    if (distanceTraveledSinceMiss >= MissDetonateDistance)
                    {
                        IsMissDetonating = true;
                        TorpedoPhysics.DetonateAndStop(missile, Vector3.up, hitArmor: false, hitTerrain: false);
                        return;
                    }
                }

                TorpedoPhysics.ApplyTorpedoPhysics(missile, hoverAltitude);
            }

            TorpedoWake.UpdateWake(missile, underwater);
        }

        private void CheckTargetMiss()
        {
            if (missile == null) return;

            Unit target = TargetRef(missile);
            MissileSeeker seeker = SeekerRef(missile);
            if (target == null && seeker != null)
            {
                try { target = SeekerTargetUnitRef(seeker); }
                catch { }
            }

            if (target != null && !target.disabled)
            {
                Vector3 toTarget = target.transform.position - missile.transform.position;
                float horizDist = new Vector2(toTarget.x, toTarget.z).magnitude;
                closestTargetDistance = Mathf.Min(closestTargetDistance, horizDist);

                Vector3 forward = missile.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                toTarget.y = 0f;

                // If torpedo was close to target (< 350m) and now target is in rear hemisphere (passed it)
                if (closestTargetDistance < 350f && Vector3.Dot(toTarget, forward) < 0f)
                {
                    TriggerMiss(forward);
                }
            }
            else
            {
                GlobalPosition gp = AimPointRef(missile);
                Vector3 toAim = gp - missile.GlobalPosition();
                toAim.y = 0f;
                float dist = toAim.magnitude;
                closestTargetDistance = Mathf.Min(closestTargetDistance, dist);

                Vector3 forward = missile.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();

                if (closestTargetDistance < 300f && Vector3.Dot(toAim, forward) < 0f)
                {
                    TriggerMiss(forward);
                }
            }
        }

        private void TriggerMiss(Vector3 forward)
        {
            HasMissed = true;
            MissForward = forward;
            distanceTraveledSinceMiss = 0f;
            TorpedoPlugin.ModLogger?.LogInfo($"[Torpedo] {missile.name} missed target. Swimming straight 100m before self-detonating.");
        }

        private void GuideBoosterTowardsTarget()
        {
            if (missile == null || missile.rb == null) return;

            Vector3 targetDir = Vector3.zero;
            Unit target = TargetRef(missile);
            MissileSeeker seeker = SeekerRef(missile);
            if (target == null && seeker != null)
            {
                try { target = SeekerTargetUnitRef(seeker); }
                catch { }
            }

            if (target != null && !target.disabled)
            {
                targetDir = target.transform.position - missile.transform.position;
            }
            else
            {
                GlobalPosition gp = AimPointRef(missile);
                if (gp != default(GlobalPosition))
                {
                    targetDir = gp - missile.GlobalPosition();
                }
            }

            // Target bearing on water
            Vector3 horizDir = new Vector3(targetDir.x, 0f, targetDir.z);
            if (horizDir.sqrMagnitude > 4f)
            {
                horizDir.Normalize();

                // Pitch angle: when high above water or starting descent, smoothly pitch down 15-25 degrees towards water entry
                float alt = (float)missile.GlobalPosition().y;
                float pitchAngle = alt > 50f ? 20f : 12f;
                Vector3 desiredForward = Vector3.RotateTowards(horizDir, Vector3.down, pitchAngle * Mathf.Deg2Rad, 0f);

                // Smoothly turn towards target bearing (75 deg/s)
                Quaternion targetRot = Quaternion.LookRotation(desiredForward, Vector3.up);
                Quaternion nextRot = Quaternion.RotateTowards(missile.transform.rotation, targetRot, 75f * Time.fixedDeltaTime);
                missile.rb.MoveRotation(nextRot);

                // Align velocity vector with forward direction so it does not drift awkwardly sideways in air
                float currentSpeed = missile.rb.velocity.magnitude;
                if (currentSpeed > 1f)
                {
                    missile.rb.velocity = Vector3.RotateTowards(missile.rb.velocity, missile.transform.forward * currentSpeed, 6f * Time.fixedDeltaTime, 0f);
                }
            }

            missile.rb.angularVelocity = Vector3.zero;
        }

        private float GetHoverAltitude()
        {
            return TorpedoCombatRules.TryGetHoverAltitude(missile, out float altitude) ? altitude : -1f;
        }

        internal static TorpedoRuntimeController EnsureAndTick(Missile liveMissile)
        {
            if (liveMissile == null) return null;
            TorpedoRuntimeController controller = liveMissile.GetComponent<TorpedoRuntimeController>();
            if (controller == null)
                controller = liveMissile.gameObject.AddComponent<TorpedoRuntimeController>();
            controller.missile = liveMissile;
            controller.Tick();
            return controller;
        }

        private void StopLaunchMotor()
        {
            launchMotorStopped = true;
            try { Traverse.Create(missile).Field("engineCurrentThrust").SetValue(0f); }
            catch { }

            object motorsObject = Traverse.Create(missile).Field("motors").GetValue();
            if (motorsObject is Array motors)
            {
                foreach (object motor in motors)
                {
                    if (motor == null) continue;
                    Traverse motorTraverse = Traverse.Create(motor);
                    motorTraverse.Field("fuelMass").SetValue(0f);
                    try { motorTraverse.Method("Burnout", new object[] { true }).GetValue(); }
                    catch { }
                }
            }

            // Ship launchers can carry a detachable VLS booster. Burnout is its normal,
            // self-contained separation path and this object belongs only to this torpedo.
            VLSBooster booster = missile.GetComponentInChildren<VLSBooster>();
            if (booster != null && missile.boosterIsAttached)
            {
                try { booster.Burnout(); }
                catch { }
            }
        }

        private void OnDestroy()
        {
            if (missile != null) TorpedoWake.RemoveWake(missile);
        }
    }
}
