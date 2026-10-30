# Bazooka

Clones PEAK's AntiZooka into a bazooka. Same model, hands, and muzzle flash — click to fire an exploding rocket instead of an antigravity bubble. You ragdoll from your own blast by default; `TF2RocketJump = true` is a TF2 soldier rocket jump (WASD, gravity still pulls, no tumble, no self-Injury).

It does not replace the vanilla AntiZooka.

## Requirements

- BepInExPack for PEAK
- [PEAKLib Core](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Core/)
- [PEAKLib Items](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Items/)

## Where it spawns

- **Roots:** at least two random luggage always pack a bazooka.
- **Other biomes:** rare luggage rolls.
- **Ammo:** `Shots` rockets per bazooka (default 8). Set to **0** for infinite. No campfire refill.

## Configuration

After one launch, edit `BepInEx/config/PeakBazooka.cfg` (no restart; saves apply while PEAK is running). Host-synced in multiplayer: `Shots`, `ExplosionOneShot`. Per player: recoil, carry weight, `ExplosionRadius` (hits and fireball, default 1.5m), volume, rocket speed, self-damage, self-knockback, `TF2RocketJump`. `Shots = 0` is infinite. Explosions always hit everyone; one rocket knocks them out unless you turn `ExplosionOneShot` off.

## Install

Install the Thunderstore package **Bazooka** with r2modman / Gale, or drop `PeakBazooka.dll` into `BepInEx/plugins/FakeGameDevelopers-Bazooka/`. Launch PEAK from the mod manager.
