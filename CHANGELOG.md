# Torpedo Balanced changelog

This build modifies [SonPamungkas's original Torpedo mod](https://github.com/SonPamungkas/torpedo).

## 1.4.9

- Reduce the cloned missile's VLS booster thrust to 15% of its source value.
- End the launch impulse after 0.8 seconds or on water entry.
- Restore gravity after the launch impulse and cap airborne speed at 90 m/s so torpedoes fall into the water instead of gliding above it.
- Confirm that the mod builds with BepInEx, Harmony, and Nuclear Option 0.34.1 assemblies; no other mod is needed for its core features.

## 1.4.8

- Balance underwater cruise speeds, warheads, and launch ranges.
- Guard AI and player launches with the same range and alignment limits.
- Fix an invalid `Missile.timeSinceSpawn` field lookup by tracking elapsed time locally.
