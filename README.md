# Torpedo

This is **Torpedo Balanced**, a community modification of the original
[Torpedo mod by SonPamungkas](https://github.com/SonPamungkas/torpedo).
The original mod and its four torpedo variants were created by SonPamungkas;
XBarni999 maintains only the balance and behavior changes in this build.

Current balanced version: **1.4.10**. See [CHANGELOG.md](CHANGELOG.md).

https://github.com/user-attachments/assets/61b5fd6c-d22f-40bf-add1-6f6740428b9e

Adds four torpedo variants to Nuclear Option (cloned from vanilla anti-ship and cruise missiles). Fire them at range like any other weapon — they drop to the water, and swim toward the target before detonating on impact or proximity to a ship.

## What it adds

### **SCT-350 'Mako'** — Fast, super-cavitating interceptor. For both anti-ship and counter-torpedo.
<img width="1671" height="1080" alt="Torp1" src="https://github.com/user-attachments/assets/8dfadfc8-dae0-4ef2-96fc-949d820743b0" />

### **Type-88 'Lemon'** — Light, compact, cheap, agile, and deceptively lethal, Ideal choice for saturation or ambush.
<img width="1670" height="1080" alt="Torp2" src="https://github.com/user-attachments/assets/2d890f87-964c-45d2-b9c3-1c55f9f3d840" />

### **HT-200 'Hammerhead'** — Heavy kinetic penetrator. Low velocity, but one is enough to split a carrier in half.
<img width="1668" height="1080" alt="Torp3" src="https://github.com/user-attachments/assets/1154659d-4843-407a-9019-c9ee3f868bb6" />

### **NT-2 'Megalodon' (20kt)** — Nuclear-tipped. Erasing the target ship and its escorts in a single delivery.
<img width="1671" height="1080" alt="Torp4" src="https://github.com/user-attachments/assets/941e4974-391d-433a-b896-2370c325ff8a" />

Every vanilla mount configuration for each source missile (single, double, internal bay, external, etc.) is automatically available for its torpedo counterpart — no manual per-aircraft setup needed.

## How it behaves

Fire it like a normal bomb. It transitions into torpedo mode once it touches the water: dives to its designated depth, holds a steady course, and detonates only when it actually hits (or near) a ship. Run one into land and it'll detonate.

Speed is deliberately far below the source missile's aerial top speed (see the table below). A brief launch impulse clears the launcher; after burnout gravity pulls the torpedo toward the water. Underwater propulsion then maintains its variant's cruise speed.

| Torpedo | Original README speed | Balanced underwater target | Original warhead multiplier | Balanced warhead multiplier |
|---|---:|---:|---:|---:|
| SCT-350 'Mako' | 359 km/h | **340 km/h** | 2.00× | 0.3575× |
| Type-88 'Lemon' | 257 km/h | 306 km/h | 2.50× | 0.1625× |
| HT-200 'Hammerhead' | 204 km/h | 198 km/h | 3.00× | 0.65× |
| NT-2 'Megalodon' (20kt) | 194 km/h | 234 km/h | 1.00× | 1.00× |

The original speeds are the values published in SonPamungkas's README; the
balanced speeds are the current controller targets at default settings. Lemon
and Megalodon are faster than that original table. The warhead multipliers are
relative to each vanilla donor missile, so they should not be read as kilograms
of TNT. Megalodon's nuclear yield remains 20 kt.

## Requirements

- BepInEx 5.4.23.5
- Nuclear Option 0.34.1 (the version used for this build)
- No dependency on other mods — the mod clones and modifies vanilla assets directly. (Which made it client-side and available on multiplayer.)

## Optional
- [Surface Loadout](https://github.com/SonPamungkas/surface-loadout) if you want naval or ground unit to fire the torpedo that you want.

### Argus Lemon
<img width="1427" height="862" alt="Argus Lemon" src="https://github.com/user-attachments/assets/c536c087-edc2-455b-b8c8-91e0a5ec9581" />

## Installation

Drop `Torpedo.dll` into your `BepInEx/plugins` folder.

## Balanced build configuration

This derivative build keeps the original four variants but changes their cruise speeds,
explosive power, penetration, costs, and useful engagement range. It also applies a launch cone to both
players and `CombatAI`; bots therefore do not select or launch a torpedo outside the
same range and off-boresight limits seen by the player.

| Variant | Original cost | Balanced cost | Original bonus penetration | Balanced bonus penetration | Balanced max launch range |
|---|---:|---:|---:|---:|---:|
| Mako | 12.5 | 18 | 500 | 56.25 | 60 km |
| Lemon | 10.5 | 14 | 500 | 0 | 35 km |
| Hammerhead | 12.5 | 30 | 4000 | 225 | 90 km |
| Megalodon | 12.5 | 80 | 0 | 0 | 120 km |

These bonus penetration values are added to the donor weapon's base value.
The original mod did not impose these explicit torpedo-specific launch caps.
The balanced build also gives player and AI launchers the same 35° acquisition
half-angle by default, adds torpedo detection/combat rules, and uses a short
0.8-second VLS launch impulse followed by gravity and water-entry propulsion.
Airborne speed is not artificially capped. Ship launch requires a compatible
ship loadout such as the original mod's optional Surface Loadout integration;
this build stabilizes torpedoes launched through those mounts.

After the first launch, edit:

`BepInEx/config/neutral.torpedo.cfg`

```ini
[Balance]

# Additional multiplier, relative to 0.85, applied to actual underwater cruise.
SpeedScale = 0.85

# Additional multiplier for blast damage and the prefab's blast yield.
WarheadScale = 0.65

# Multiplier for each variant's bonus armor penetration.
PenetrationScale = 0.75

# Relative to 0.60; applied to launch range and physical motor endurance.
RangeScale = 0.60

# Maximum launch angle away from the aircraft/launcher nose, in degrees.
# The player targeting system and CombatAI read the same value.
AcquisitionHalfAngle = 35
```

Restart the game after changing these values because torpedo prefabs are created when
the encyclopedia is loaded.

## Custom model requirements

The original mod clones vanilla missile prefabs, so its current shapes are placeholders.
For replacement art, provide one model per desired variant in FBX or OBJ format, plus
PNG/TGA textures and a note describing the model's license/source. Keep moving propellers
or control surfaces as separately named objects if they should be animated. Models will
be imported with their forward axis aligned to Unity `+Z`, up on `+Y`, and their pivot at
the torpedo's center of mass. An AssetBundle-based loader is preferred over a runtime OBJ
parser because it preserves materials, LODs, colliders and animation transforms.

## Known limitations
- VLS and air launch behavior still needs verification in a live mission.
- Torpedoes are generally undetectable once it dives, but can still be stopped by explosion on the surface or an obstacle in its path.
- Torpedoes target whatever the underlying missile's seeker would normally target (they don't yet filter to naval-only targets).

## Build from source

The game assemblies are referenced from a local Nuclear Option 0.34.1 installation
and are not redistributed. With .NET Framework 4.7.2 targeting support installed:

```powershell
dotnet build Torpedo.csproj -c Release -p:GameDir="F:\path\to\Nuclear Option"
```

The output is `bin/Release/net472/Torpedo.dll`.
