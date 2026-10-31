# peak-bazooka

Clones PEAK's AntiZooka into a bazooka. Same model, hands, and muzzle flash — click to fire an exploding rocket instead of an antigravity bubble.

A click launches one rocket. There are two modes (`Mode` in the config, the host's choice in multiplayer):

- **Standard** — a weapon. The blast injures and ragdolls other scouts. Your own blast flings you as a ragdoll and hurts you too (`SelfDamage`, a quarter of your health at point-blank).
- **TF2** — a TF2 soldier's rocket launcher. Rockets **never hurt scouts**, you or anyone else. Your own blast is a rocket jump: you keep your momentum, steer and brake with WASD, stay on your feet, and take no fall damage when you land. Shoot the floor to go up, the floor behind you to go forward, a wall to go sideways. Rockets fired mid-air add up, so jumps chain.

Creatures are hit in both modes, and each kind can be switched off under `[Targets]` (all on by default). One rocket kills a spider, beetle, scorpion, frog, condor or antlion and knocks out a zombie. Scoutmaster Myres takes 3 rockets (`ScoutmasterHits`): each one knocks him down, the last sends him away for the rest of the map. Rockets also smash jellyfish, beehives, mandrakes, spore bombs and dynamite, knock ticks off scouts and coconuts off trees, and push tumbleweeds and Bing Bong around.

The fireball is 3.5m; the push reaches 6m. Zombies, other scouts, creatures and loose items in it are thrown hard (`Knockback`, 30 m/s in the middle); knockback and Injury are strongest in the middle and fade toward the edge. Like any explosion in PEAK, it cooks the food and other items lying in it.

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
- **Ammo:** infinite by default (`Shots = 0`). Any other number is a limited magazine with no refill.

## Configuration

After one launch, edit `BepInEx/config/PeakBazooka.cfg`:

| Section | Key | Default | Meaning |
|---|---|---|---|
| `[Bazooka]` | `Mode` | `Standard` | `Standard` (a weapon) or `TF2` (no scout damage, rocket jumps). Host's value |
| `[Bazooka]` | `Shots` | `0` | Rockets per bazooka. **0 = infinite**, otherwise limited. Host's value |
| `[Bazooka]` | `Recoil` | `6` | Recoil strength in m/s (Standard). **0** = none |
| `[Bazooka]` | `CarryWeight` | `4` | Stamina-bar weight (1 = 2.5) |
| `[Bazooka]` | `ExplosionRadius` | `3.5` | Fireball radius in metres. Hits reach this far, but at least 6m |
| `[Bazooka]` | `Knockback` | `30` | How hard a blast throws zombies, other scouts, creatures and loose items, m/s in the middle (40% at the edge). The shooter's value is used |
| `[Bazooka]` | `ExplosionVolume` | `0.2` | Explosion loudness vs dynamite |
| `[Bazooka]` | `RocketSpeed` | `50` | Rocket flight speed in m/s |
| `[Bazooka]` | `SelfDamage` | `0.25` | Standard: Injury from your own blast at point-blank (0–1), half at the edge |
| `[Bazooka]` | `SelfKnockback` | `22` | Standard: how hard your own blast flings you, m/s at point-blank |
| `[Bazooka]` | `SelfKnockbackUp` | `0.75` | Standard: extra upward pull so a floor shot pops you up |
| `[Bazooka]` | `GiveBazookaOnSpawn` | `false` | Host: one bazooka in hand per scout on climb spawn |
| `[TF2]` | `LaunchSpeed` | `20` | Rocket jump speed, m/s at point-blank (half at 6m) |
| `[TF2]` | `LaunchUp` | `0.6` | Extra upward pull on a rocket jump. 0 = straight away from the blast |
| `[TF2]` | `AirControl` | `10` | How fast WASD steers / brakes in the air, m/s² |
| `[TF2]` | `Gravity` | `10` | Gravity during a rocket jump, m/s². Lower = floatier |
| `[Targets]` | `Scouts`, `Zombies`, `Scoutmaster`, `Spiders`, `Beetles`, `Scorpions`, `Frogs`, `Condors`, `Antlions`, `Ticks`, `Jellyfish`, `BeeSwarms`, `Beehives`, `Mandrakes`, `Spores`, `Dynamite`, `Coconuts`, `Tumbleweeds`, `BingBong`, `Balloons` | `true` | Whether rockets affect that target. Host's value. `Scouts` only matters in Standard |
| `[Targets]` | `ScoutmasterHits` | `3` | Rockets to get rid of the Scoutmaster. Host's value |
| `[Debug]` | `EnableDebugMode` | `false` | Host spawns test zombies, a bazooka, and luggage at Airport / Shore |

In multiplayer `Mode`, `Shots` and everything under `[Targets]` come from the host. Recoil, explosion size and loudness, self-damage, self-knockback and the `[TF2]` jump tuning stay per player. Saving `PeakBazooka.cfg` while PEAK is running applies on the next shot (about half a second); no restart. An old `TF2RocketJump = true` (or `SelfRagdoll = false`) becomes `Mode = TF2`; `ExplosionOneShot` is gone (every creature dies in one rocket except the Scoutmaster).

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
