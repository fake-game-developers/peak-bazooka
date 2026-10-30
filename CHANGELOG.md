# Changelog

## 0.1.13

- Blast radius matches the fireball (`ExplosionRadius` default **1.5m**; was 6m around a much smaller explosion). `ExplosionVisualScale` is gone — change `ExplosionRadius` if you want a bigger boom
- Floor rocket jumps still launch: self-knockback is measured from your feet, not chest

## 0.1.12

- `TF2RocketJump` (default off): WASD rocket jump, no ragdoll, **0 self-Injury** (and no fall Injury from the jump)
- `SelfRagdoll = false` in an old cfg is migrated to `TF2RocketJump = true`

## 0.1.11

- Drop the `[Shootables]` section. Explosions always hit scouts, zombies, and every other blast target

## 0.1.10

- Drop `CanSpawnOnAnyBiome` (shotgun leftover). The bazooka always has rare luggage rolls in every biome, plus two guaranteed Roots suitcases

## 0.1.9

- Rocket jumps no longer apply ~30% fall Injury on landing (`SelfRagdoll` off)

## 0.1.8

- Ground rocket jumps arc and fall (no more floating up until you hit a ceiling). WASD still steers when `SelfRagdoll` is off

## 0.1.7

- Drop the `[HitsToDefeat]` section (zombies showing 2 hits while one rocket already KOs). `ExplosionOneShot` is the switch
- Drop `EnableRecoil`; set `Recoil = 0` to turn recoil off
- `[Shootables]` copy now says rockets, not shotguns

## 0.1.6

- Dropped bazookas fall like normal items (no AntiZooka hover)

## 0.1.5

- Smaller, quieter explosion (`ExplosionVisualScale` 0.3, `ExplosionVolume` 0.2 vs dynamite)
- `SelfRagdoll = false` is a jetpack-style rocket jump: WASD while airborne, no tumble, no self-Injury
- Self-damage only applies when ragdoll is on

## 0.1.4

- Own-blast ragdolls you by default (`SelfRagdoll = true`). Set it `false` for WASD air control
- Config edits apply on the next shot / without restarting PEAK (cfg is polled while the game is running)

## 0.1.3

- Explosions actually hit zombies, scouts, and world targets (no longer skipped when the blast overlaps terrain)
- One rocket knocks out / destroys a shootable by default (`ExplosionOneShot`; off = one hit, then `HitsToDefeat`)
- `[Shootables]` still gates what the blast can hurt

## 0.1.2

- Fire on click: no AntiZooka charge bar, charge SFX, or wait before the rocket leaves
- Rocket jumps keep WASD air control (no ragdoll / Fall from your own blast)
- `Shots` is back (default 8). **0 = infinite**

## 0.1.1

- Remove campfire ammo piles, the magazine, and reloads. Charge and fire; shots are unlimited

## 0.1.0

- Thunderstore package name is **Bazooka** (plugin / assembly stay `PeakBazooka`)
- Rebuild as a bazooka cloned from PEAK's AntiZooka instead of a shotgun cloned from the blowgun
- Drops the custom mesh, arm IK, and hold-pose hacks; the in-game AntiZooka model and hands are the baseline
- Charge-and-fire like the AntiZooka, but the round is a flying rocket that explodes on impact instead of an antigravity bubble
- Blast uses dynamite explosion VFX and still hits the same shootable targets (scouts, zombies, frogs, and so on)
- Config section is now `[Bazooka]` (recoil, carry weight, explosion radius, rocket speed)
- Own rockets launch you (distance-scaled knockback) with little or no self-Injury (`SelfDamage`, default 0.05, 0 = none) so you can rocket-jump like a TF2 soldier
