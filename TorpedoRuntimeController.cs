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
        private Missile missile;
        private bool launchMotorStopped;
        private bool enteredWater;
        private float lastPhysicsTick = float.NegativeInfinity;
        private float elapsedLifetime;
        private float boosterBurnTime;

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
                StopLaunchMotor();

            if (missile.rb != null && !missile.rb.isKinematic)
            {
                // Vanilla missiles disable gravity. After the short launch impulse,
                // restore it so aircraft drops and VLS launches enter the sea.
                missile.rb.useGravity = launchMotorStopped && !underwater;
                // Ship-launched torpedoes start nose-up. Suppress the donor missile's
                // aggressive air steering and clear inherited angular motion.
                if (!underwater && missile.LocalSim)
                    missile.rb.angularVelocity = Vector3.zero;
            }

            if (!TorpedoCombatRules.TryGetHoverAltitude(missile, out float hoverAltitude)) return;
            if (underwater)
                TorpedoPhysics.ApplyTorpedoPhysics(missile, hoverAltitude);
            TorpedoWake.UpdateWake(missile, underwater);
        }

        private float GetHoverAltitude()
        {
            return TorpedoCombatRules.TryGetHoverAltitude(missile, out float altitude) ? altitude : -1f;
        }

        internal static void EnsureAndTick(Missile liveMissile)
        {
            if (liveMissile == null) return;
            TorpedoRuntimeController controller = liveMissile.GetComponent<TorpedoRuntimeController>();
            if (controller == null)
                controller = liveMissile.gameObject.AddComponent<TorpedoRuntimeController>();
            controller.missile = liveMissile;
            controller.Tick();
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
