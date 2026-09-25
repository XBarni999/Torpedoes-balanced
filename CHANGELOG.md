# Torpedo Balanced changelog

This build modifies [SonPamungkas's original Torpedo mod](https://github.com/SonPamungkas/torpedo).

## 1.4.11

- **Miss & Overshoot Behavior**: If a torpedo misses/overshoots its target, it no longer does an unrealistic 180-degree turn to re-attack. Instead, it locks heading forward, swims straight for ~100 meters, and safely self-detonates.
- **Ship Launch Booster**: Increased VLS booster thrust (from 15% to 50%) and booster burn time (2.0s) with active pitch-assist after tube exit so torpedoes easily clear ship launchers and deck without falling back onto the ship.
- **Aircraft Launch Protection**: Aircraft drops use a short 0.4s drop impulse and enforce a strict 90 m/s (~324 km/h) airborne speed cap, ensuring aircraft-dropped torpedoes never accelerate towards Mach.
- **Flak & Direct Hit Fixes**: Proximity airburst explosions and surface flak above water no longer damage submerged torpedoes or generate false "torpedo destroyed" notifications while swimming. Only authorized direct kinetic gun impacts can damage or destroy torpedoes.
- **Naval Cannon Defense**: Expanded gun detection to include heavy naval guns, cannons, and batteries, and adjusted minimum defense range so capital ship large guns can engage incoming torpedoes.

## 1.4.10

- Remove the artificial airborne speed cap; VLS thrust remains reduced and burns for 0.8 seconds after activation.
- Suppress donor missile air steering and angular spin during vertical ship launches; normal steering resumes underwater.
- Lower Mako's default underwater target to 94.4 m/s (about 340 km/h).
- Make SpeedScale and RangeScale affect actual underwater cruise and launch range while preserving existing default values.
- Add an explicit comparison with the original mod's published speeds, warhead multipliers, penetration bonuses, costs, and ship-launch requirements to README.

## 1.4.9

- Reduce the cloned missile's VLS booster thrust to 15% of its source value.
- End the launch impulse after 0.8 seconds or on water entry.
- Restore gravity after the launch impulse. The temporary 90 m/s airborne cap was removed in 1.4.10.
- Confirm that the mod builds with BepInEx, Harmony, and Nuclear Option 0.34.1 assemblies; no other mod is needed for its core features.

## 1.4.8

- Balance underwater cruise speeds, warheads, and launch ranges.
- Guard AI and player launches with the same range and alignment limits.
- Fix an invalid `Missile.timeSinceSpawn` field lookup by tracking elapsed time locally.
