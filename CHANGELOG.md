# Changelog

## 1.0.0

First Thunderstore release.

- **Two modes** (`Mode`, host's choice): **Standard** is a weapon (rockets injure and fling scouts, your own blast hurts you, `SelfDamage` 0.25); **RocketJump** never hurts scouts and turns your own blast into a rocket jump you steer with WASD, with no fall damage. Rockets fired while falling cancel the fall and launch you again, so jumps chain
- **Targets:** `[Targets]` has one switch per target, all on. One rocket kills spiders, beetles, scorpions, frogs, condors and antlions and knocks out zombies; the Scoutmaster takes `ScoutmasterHits` (3)
- **Blast:** sized to the fireball instead of dynamite's range (no more "nuke"). `Knockback` (30 m/s) throws zombies, scouts, creatures and loose items, out to 6m; the blast cooks items lying in it, like any PEAK explosion. Smoke clears after 7 seconds
- **Rockets:** PEAK's own firework rocket model in flight; they hit what is right in front of you and keep flying if you put the bazooka away
- **Look:** red fittings and a white tube (grips keep the AntiZooka's brass); no antigravity disc in the muzzle or bubbles when firing; the gun settles back where you hold it after the recoil
- **Ammo:** `Shots` defaults to 0 = infinite
- **Config:** `RocketSpeed` is now `MuzzleVelocity` (it also sets how far rockets fly); the `[TF2]` section is now `[RocketJump]`. Old values move over automatically. New defaults: `ExplosionRadius` 3.5, `ExplosionVolume` 0.5, `MuzzleVelocity` 50, `SelfKnockback` 22
- **Multiplayer:** every player needs this version

## 0.1.0 – 0.1.13

- Pre-release builds, never published
