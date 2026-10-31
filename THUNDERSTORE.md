# Bazooka

**Point. Click. Boom.** A red-and-white rocket launcher for PEAK. Blast zombies off the mountain, wipe out the critters that ruin your climb, launch your friends into the sky, or rocket jump your way to the summit.

![Holding the bazooka in first person](https://raw.githubusercontent.com/fake-game-developers/peak-bazooka/master/media/img1.jpg)

## Features

- 🚀 **Click to fire.** A real rocket flies out and explodes on whatever it hits.
- 💥 **Big blasts.** Zombies, scouts, creatures and loose items go flying, and food caught in the blast gets cooked.
- 🕷️ **One-shot pests.** Spiders, beetles, scorpions, frogs, condors and antlions die to a single rocket. Zombies stay down.
- 🧢 **Scoutmaster Myres** takes 3 rockets. The last one sends him away for the rest of the map.
- 🦘 **RocketJump mode.** No damage to scouts. Shoot the floor and fly, TF2 style.
- 🎒 **Spawns like the AntiZooka**, in the same places and just as often. The vanilla AntiZooka is untouched.
- ⚙️ **Fully configurable** and changeable mid-game, with host-synced rules in multiplayer.

## Quick start

1. Install with **r2modman** or **Gale**. The dependencies come with it.
2. Start PEAK **from the mod manager** (starting from Steam skips the mods).
3. Find a bazooka, or set `GiveBazookaOnSpawn = true` to start every run with one.
4. Press primary fire.

In multiplayer, **everyone needs the mod**, on the same version.

## Pick your mode

Set `Mode` under `[Bazooka]`. The host's choice applies to the whole lobby, and you can switch mid-run.

| | 💣 **Standard** (default) | 🦘 **RocketJump** |
|---|---|---|
| **Use it as** | A weapon | A way to fly up the mountain |
| **Other scouts** | Hurt and flung | Never hurt or pushed |
| **Your own blast** | Ragdolls you and costs up to a quarter of your health | Launches you on your feet, no damage, no fall damage |
| **In the air** | You are a ragdoll | Steer and brake with WASD |
| **Creatures and zombies** | Hit | Hit |

**Rocket jump tips:** shoot the floor to go up, behind you to go forward, a wall to go sideways. Shoot the ground again while falling to cancel the fall and chain jumps up a cliff.

![A scout carrying the bazooka on the Shore](https://raw.githubusercontent.com/fake-game-developers/peak-bazooka/master/media/img2.jpg)

## What you can blow up

| Target | What a rocket does |
|---|---|
| Zombies | Knocked out for good |
| Other scouts | Injured and flung (Standard only) |
| Spiders, condors | Killed, and they drop whoever they were holding |
| Beetles, scorpions, antlions | Killed |
| Gloom frogs | Killed, and they drop cooked frog legs |
| Scoutmaster Myres | Knocked down; gone after 3 rockets |
| Jellyfish, beehives, mandrakes, spore bombs | Smashed |
| Spore clouds, bee swarms | Cleared and scattered |
| Dynamite | Set off |
| Ticks, coconuts | Knocked off scouts and palm trees |
| Tumbleweeds, Bing Bong, loose items | Sent flying |
| Balloons | Popped |

Each bazooka holds **5 rockets** and cannot be refilled. Set `Shots = 0` for infinite ammo.

## Settings

Edit `BepInEx/config/PeakBazooka.cfg` (created on first launch) or use [ModConfig](https://thunderstore.io/c/peak/p/PEAKModding/ModConfig/) in-game. Changes apply on the next shot, no restart needed.

**The ones you will want:**

| Key | Default | What it does |
|---|---|---|
| `Mode` | `Standard` | `Standard` or `RocketJump` (host) |
| `Shots` | `5` | Rockets per bazooka. `0` = infinite (host) |
| `GiveBazookaOnSpawn` | `false` | Every scout starts the climb with a bazooka (host) |
| `Knockback` | `30` | How hard blasts throw things, m/s |
| `ExplosionRadius` | `3.5` | Fireball size in metres (push reaches at least 6m) |
| `ExplosionVolume` | `0.5` | Loudness compared with dynamite |

**Fine-tuning:**

| Section | Key | Default | What it does |
|---|---|---|---|
| `[Bazooka]` | `MuzzleVelocity` | `50` | Rocket speed, m/s. Rockets drop slightly and burn out after 6s |
| `[Bazooka]` | `Recoil` | `6` | Kickback when firing (Standard). `0` = none |
| `[Bazooka]` | `CarryWeight` | `4` | Weight on the stamina bar (4 = 10 on the bar) |
| `[Bazooka]` | `SelfDamage` | `0.25` | Standard: damage from your own blast at point-blank (0–1) |
| `[Bazooka]` | `SelfKnockback` | `22` | Standard: how hard your own blast flings you, m/s |
| `[Bazooka]` | `SelfKnockbackUp` | `0.75` | Standard: extra upward pop from your own blast |
| `[RocketJump]` | `LaunchSpeed` | `20` | Rocket jump speed at point-blank, m/s |
| `[RocketJump]` | `LaunchUp` | `0.6` | Extra upward pull. `0` = straight away from the blast |
| `[RocketJump]` | `AirControl` | `10` | How fast WASD steers in the air, m/s² |
| `[RocketJump]` | `Gravity` | `10` | Gravity while flying, m/s². Lower = floatier |

**Turn targets off:** every target has its own switch under `[Targets]`, all on by default: `Scouts`, `Zombies`, `Scoutmaster`, `Spiders`, `Beetles`, `Scorpions`, `Frogs`, `Condors`, `Antlions`, `Ticks`, `Jellyfish`, `BeeSwarms`, `Beehives`, `Mandrakes`, `Spores`, `Dynamite`, `Coconuts`, `Tumbleweeds`, `BingBong` and `Balloons`. `ScoutmasterHits` (default `3`, 1–10) sets how many rockets the Scoutmaster takes.

**Multiplayer:** `Mode`, `Shots`, `GiveBazookaOnSpawn` and `[Targets]` come from the host. `Knockback` is the shooter's. Everything else is per player.

![The bazooka lying on the beach](https://raw.githubusercontent.com/fake-game-developers/peak-bazooka/master/media/img3.jpg)

## Translations

The bazooka's name follows PEAK's language setting (all 15 languages). To rename it, create `BepInEx/config/PeakBazooka.localization.csv`:

```csv
Key,English,German
NAME_BAZOOKA,BAZOOKA,PANZERFAUST
```

Language columns: `English`, `French`, `Italian`, `German`, `SpanishSpain`, `SpanishLatam`, `BRPortuguese`, `Russian`, `Ukrainian`, `SimplifiedChinese`, `TraditionalChinese`, `Japanese`, `Korean`, `Polish`, `Turkish`. Empty cells keep the built-in text.

## Manual install

Put `PeakBazooka.dll` in `BepInEx/plugins/FakeGameDevelopers-Bazooka/`, next to [BepInExPack for PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/), [PEAKLib Core](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Core/) and [PEAKLib Items](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Items/).

## Bugs and ideas

Post them on [GitHub Issues](https://github.com/fake-game-developers/peak-bazooka/issues). It helps to include what happened, whether you were host or client, your `Mode`, the biome, your mod version and `BepInEx/LogOutput.log` (r2modman: Settings → Debugging → Copy log file contents to clipboard).

## Credits

Made by **Arman Ossi Loko** for **Fake Game Developers**. The gun code started from [PeakShotgun](https://github.com/fake-game-developers/peak-shotgun), which is based on [Peak_AKGun](https://github.com/TheCodinPro/Peak_AKGun) by TheCodinPro. Licensed under [MIT with attribution](https://github.com/fake-game-developers/peak-bazooka/blob/master/LICENSE).
