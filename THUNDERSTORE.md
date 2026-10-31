# Bazooka

Clones PEAK's AntiZooka into a bazooka. Same model, hands, and muzzle flash — click to fire an exploding rocket instead of an antigravity bubble.

- **Standard mode:** a weapon. Rockets injure and knock down scouts; your own blast flings you.
- **TF2 mode:** rockets never hurt scouts. Your own blast is a TF2 soldier rocket jump: keep your momentum, steer with WASD, no fall damage.

Spiders, beetles, scorpions, frogs, condors, antlions and zombies go down in one rocket, the Scoutmaster in three. Every target can be switched off. The blast is sized to the fireball: it reaches 4m and flings what is in it a few metres, not a dynamite launch.

It does not replace the vanilla AntiZooka.

## Requirements

- BepInExPack for PEAK
- [PEAKLib Core](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Core/)
- [PEAKLib Items](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Items/)

## Where it spawns

- **Roots:** at least two random luggage always pack a bazooka.
- **Other biomes:** rare luggage rolls.
- **Ammo:** infinite by default (`Shots = 0`). Any other number is a limited magazine with no refill.

## Configuration

After one launch, edit `BepInEx/config/PeakBazooka.cfg` (no restart; saves apply while PEAK is running). Host-synced in multiplayer: `Mode`, `Shots` and the `[Targets]` switches (all on; `ScoutmasterHits` = 3). Per player: recoil, carry weight, `ExplosionRadius` (default 1.5m), volume, rocket speed, Standard self-blast (`SelfDamage`, `SelfKnockback`) and the `[TF2]` rocket jump tuning (`LaunchSpeed`, `LaunchUp`, `AirControl`, `Gravity`).

## Install

Install the Thunderstore package **Bazooka** with r2modman / Gale, or drop `PeakBazooka.dll` into `BepInEx/plugins/FakeGameDevelopers-Bazooka/`. Launch PEAK from the mod manager.
