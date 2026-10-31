# Changelog

## 1.0.0

First release.

- **Bazooka:** a red-and-white rocket launcher built from PEAK's AntiZooka (the vanilla AntiZooka is untouched). Click to fire a rocket, PEAK's own firework model, that explodes on the first thing it touches, even if you put the bazooka away mid-flight
- **Two modes** (`Mode`, the host's choice): **Standard**, the default, is a weapon: rockets injure and fling scouts and your own blast hurts you (`SelfDamage` 0.25). **RocketJump** never hurts scouts and turns your own blast into a rocket jump you steer with WASD, with no fall damage; a rocket under you while falling launches you again, so jumps chain
- **Blast:** a 3.5m fireball whose push reaches 6m (`Knockback` 30 m/s): zombies, scouts, creatures and loose items go flying, and items lying in it get cooked like in any PEAK explosion
- **Targets:** one rocket kills spiders, beetles, scorpions, frogs, condors and antlions and knocks out zombies; the Scoutmaster takes 3 (`ScoutmasterHits`). Rockets also hit jellyfish, beehives, mandrakes, spores, dynamite, bee swarms, ticks, coconuts, tumbleweeds, Bing Bong and balloons. Every target has its own switch under `[Targets]`, all on
- **Finding it:** at least two suitcases in the Roots every run, rare rolls elsewhere (at most 4 per biome), or `GiveBazookaOnSpawn` for everyone at the start
- **Ammo:** infinite by default (`Shots = 0`)
- **Multiplayer:** `Mode`, `Shots` and `[Targets]` come from the host; every player needs the mod
- Configs from pre-release builds update themselves (`RocketSpeed` → `MuzzleVelocity`, `[TF2]` → `[RocketJump]`, `Mode = TF2` → `RocketJump`)

## 0.1.0 – 0.1.13

- Pre-release builds, never published
