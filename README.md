# peak-bazooka

Clones PEAK's AntiZooka into a bazooka. Same model, hands, and muzzle flash — click to fire an exploding rocket instead of an antigravity bubble.

A click launches one rocket. The blast injures other scouts and knocks them back. Your own blast mostly launches you instead. You ragdoll from that blast unless you set `TF2RocketJump = true` — then it is a TF2 soldier rocket jump (WASD in the air, gravity still pulls, no tumble, **no self-Injury**). `SelfDamage` only applies when that is off. Zombies ignore Injury: the first hit flips them, the second knocks them out. You can also blast condors, antlions, Gloom frogs, beehives, ticks, jellyfish, coconuts, bee swarms, tumbleweeds, balloons, dynamite, and Bing Bong.

It does not replace the vanilla AntiZooka.

## Requirements

- BepInExPack for PEAK
- [PEAKLib Core](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Core/)
- [PEAKLib Items](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Items/)

## Where it spawns

- **On spawn (optional):** set `GiveBazookaOnSpawn = true` and the **host** puts one bazooka in each scout's hand when they enter the climb or join mid-run (Shore and later — not at the Airport).
- **Roots:** every run, **at least two random luggage** on the Roots map are forced to contain a bazooka.
- **Other biomes:** luggage can roll a bazooka (**RidiculouslyRare**).
- **Cap:** at most **4** luggage per biome can contain a bazooka in a single run.
- **Ammo:** `Shots` rockets per bazooka (default 8). Set to **0** for infinite. No campfire refill.

## Configuration

After one launch, edit `BepInEx/config/PeakBazooka.cfg`:

| Section | Key | Default | Meaning |
|---|---|---|---|
| `[Bazooka]` | `Shots` | `8` | Rockets per bazooka (host's value in multiplayer). **0 = infinite** |
| `[Bazooka]` | `Recoil` | `6` | Recoil strength in m/s. **0** = none |
| `[Bazooka]` | `CarryWeight` | `4` | Stamina-bar weight (1 = 2.5) |
| `[Bazooka]` | `ExplosionRadius` | `1.5` | Blast radius in metres (hits and fireball) |
| `[Bazooka]` | `ExplosionVolume` | `0.2` | Explosion loudness vs dynamite |
| `[Bazooka]` | `ExplosionOneShot` | `true` | One rocket KOs everyone in the blast. Off = one hit (zombies 2, Scoutmaster 4) |
| `[Bazooka]` | `RocketSpeed` | `38` | Rocket flight speed in m/s |
| `[Bazooka]` | `SelfDamage` | `0.05` | Injury from your own blast (0–1). Ignored when `TF2RocketJump` is on |
| `[Bazooka]` | `SelfKnockback` | `16` | How hard your own blast launches you, m/s at point-blank |
| `[Bazooka]` | `SelfKnockbackUp` | `0.75` | Extra upward pull so a floor shot pops you up |
| `[Bazooka]` | `TF2RocketJump` | `false` | TF2 soldier: WASD, no ragdoll, **0 self-Injury** |
| `[Bazooka]` | `GiveBazookaOnSpawn` | `false` | Host: one bazooka in hand per scout on climb spawn |
| `[Debug]` | `EnableDebugMode` | `false` | Host spawns test zombies, a bazooka, and luggage at Airport / Shore |

Explosions always hit scouts, zombies, frogs, and the rest — there is no per-target toggle. In multiplayer `Shots` and `ExplosionOneShot` come from the host. Recoil, explosion size, self-damage, self-knockback, and `TF2RocketJump` stay per player. Saving `PeakBazooka.cfg` while PEAK is running applies on the next shot (about half a second); no restart. Old `ExplosionRadius = 6` (with a smaller-looking fireball) is rewritten to match that fireball on first launch of 0.1.13.

## Install

1. Install the Thunderstore package **Bazooka** with r2modman / Gale, **or** drop `PeakBazooka.dll` into `BepInEx/plugins/FakeGameDevelopers-Bazooka/`.
2. Launch PEAK from r2modman. Starting the game from Steam skips BepInEx.

## Build

Requires [PEAK](https://store.steampowered.com/app/3527290/PEAK/) installed, or set `PEAK_GAME_DIR` / `-p:PeakGameRootDir=`.

```bash
dotnet build Peak.Shotgun.slnx -c Release
```

The DLL is `artifacts/bin/Peak.Shotgun/release/PeakBazooka.dll`.

Vanilla AntiZooka dump and concept art: `antizooka/`.

## Credits

- Original author: **Arman Ossi Loko**
- This mod belongs to **Fake Game Developers**

Work based on this mod must credit Arman Ossi Loko and Fake Game Developers. See [LICENSE](LICENSE).
