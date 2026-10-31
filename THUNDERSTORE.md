# Bazooka

![Holding the bazooka in first person](https://raw.githubusercontent.com/fake-game-developers/peak-bazooka/master/media/img1.jpg)

![A scout carrying the bazooka on the Shore](https://raw.githubusercontent.com/fake-game-developers/peak-bazooka/master/media/img2.jpg)

![The bazooka lying on the beach](https://raw.githubusercontent.com/fake-game-developers/peak-bazooka/master/media/img3.jpg)

A red-and-white bazooka built from PEAK's AntiZooka. Click to fire a rocket that explodes on impact: it throws zombies, scouts and creatures around, kills the critters that make the climb miserable, and cooks whatever food is lying in the blast. That is **Standard** mode, the default. Switch to **RocketJump** mode and it stops hurting scouts and becomes a way to fly up the mountain.

It is a new item next to the vanilla AntiZooka, which stays as it is.

Found a bug or have an idea? Report it on [GitHub Issues](https://github.com/fake-game-developers/peak-bazooka/issues).

## Two modes

The bazooka has two modes. **Standard is the default.** Pick one with `Mode` under `[Bazooka]` in `PeakBazooka.cfg` (or in ModConfig in-game): `Mode = Standard` or `Mode = RocketJump`. You can switch mid-run; it applies on the next shot. In multiplayer the **host's** mode applies to everyone.

| | **Standard** (default) | **RocketJump** |
|---|---|---|
| What it is for | A weapon | Getting around: rocket jumping in the style of TF2's soldier |
| Other scouts | Injured (up to 0.4 Injury) and flung | Never hurt or pushed |
| Your own blast | Ragdolls and flings you, and hurts you (a quarter of your health at point-blank) | Launches you on your feet: steer and brake with WASD, no damage, no fall damage when you land |
| Recoil when firing | Pushes you back | None |
| Zombies, creatures and everything else | Hit | Hit the same way |
| Settings that apply | `SelfDamage`, `SelfKnockback`, `SelfKnockbackUp`, `Recoil` | The `[RocketJump]` section |

**Standard:** stand back from your own blast. A floor shot at your feet throws you as a ragdoll, which you can use to get somewhere fast, at a cost.

**RocketJump:** rockets never hurt any scout, you included. Shoot the floor to go up, the floor behind you to go forward, a wall to go sideways. A rocket under you while you fall cancels the fall and launches you again, so you can chain jumps up a cliff.

## What rockets do

A rocket flies straight with a slight drop and explodes on the first thing it touches. It still explodes if you put the bazooka away while it is in the air.

- **The blast:** a 3.5m fireball. Its push reaches 6m and is strongest in the middle: zombies, scouts, creatures and loose items go flying. Like any explosion in PEAK, it cooks the items lying in it.
- **Zombies** are knocked out by one rocket and stay down.
- **Creatures:** one rocket kills a spider (it drops whoever it was holding), beetle, scorpion, Gloom frog (it drops cooked frog legs), Caldera condor (it drops whoever it was carrying) or Mesa antlion.
- **Scoutmaster Myres** takes 3 rockets: each one knocks him down, the last sends him away for the rest of the map.
- **Everything else:** rockets smash Shore jellyfish, beehives, mandrakes and spore bombs, clear spore clouds, set off dynamite, scatter bee swarms, knock ticks off scouts and coconuts off palm trees, push tumbleweeds and Bing Bong around, and pop balloons.

Every target can be switched off under `[Targets]`.

## Requirements

- BepInExPack for PEAK
- [PEAKLib Core](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Core/)
- [PEAKLib Items](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_Items/)

The mod builds the bazooka from the AntiZooka already in the game: same model and hands, repainted red and white, firing PEAK's own firework rocket model. Installing the mod is the whole setup. **In multiplayer, everyone needs the mod, on the same version.**

## Where to find it

- **Roots:** every run, at least two random suitcases anywhere on the Roots map contain a bazooka. A custom run that disables the bazooka in its item settings gets none.
- **Other biomes:** suitcases can roll a bazooka (**RidiculouslyRare**).
- **Cap:** at most 4 suitcases per biome contain a bazooka in one run, Roots included.
- **On spawn (optional):** set `GiveBazookaOnSpawn = true` and the **host** puts a bazooka in each scout's hand when they start the climb or join mid-run (Shore and later, not at the Airport; once per player per run).

Ammo is infinite by default (`Shots = 0`). Any other number is a magazine that cannot be refilled.

## Configuration

Edit `BepInEx/config/PeakBazooka.cfg` (it appears after one launch), or use [ModConfig](https://thunderstore.io/c/peak/p/PEAKModding/ModConfig/) in-game. Changes apply while PEAK is running, on the next shot.

| Section | Key | Default | Meaning |
|---|---|---|---|
| `[Bazooka]` | `Mode` | `Standard` | `Standard` (a weapon) or `RocketJump` (no scout damage, rocket jumps); see [Two modes](#two-modes). Host's value |
| `[Bazooka]` | `Shots` | `0` | Rockets per bazooka. `0` = infinite. Host's value |
| `[Bazooka]` | `Recoil` | `6` | How hard firing pushes you back, m/s (Standard). `0` = none |
| `[Bazooka]` | `CarryWeight` | `4` | Stamina-bar weight while carried (1 = 2.5 on the bar; 4 = 10) |
| `[Bazooka]` | `ExplosionRadius` | `3.5` | Fireball radius in metres. The push reaches this far, but at least 6m |
| `[Bazooka]` | `ExplosionVolume` | `0.5` | Explosion loudness compared with dynamite |
| `[Bazooka]` | `MuzzleVelocity` | `50` | How fast rockets leave the muzzle, m/s. Rockets drop slightly and burn out after 6s, so this also sets how far and flat they fly |
| `[Bazooka]` | `SelfDamage` | `0.25` | Standard: Injury from your own blast at point-blank (0–1), half at the edge |
| `[Bazooka]` | `Knockback` | `30` | How hard a blast throws zombies, other scouts, creatures and loose items, m/s in the middle (40% at the edge). The shooter's value is used |
| `[Bazooka]` | `SelfKnockback` | `22` | Standard: how hard your own blast flings you, m/s at point-blank |
| `[Bazooka]` | `SelfKnockbackUp` | `0.75` | Standard: extra upward pull, so a floor shot pops you up |
| `[Bazooka]` | `GiveBazookaOnSpawn` | `false` | Host: give every scout a bazooka when the climb starts |
| `[RocketJump]` | `LaunchSpeed` | `20` | Rocket jump speed, m/s at point-blank (half at 6m) |
| `[RocketJump]` | `LaunchUp` | `0.6` | Extra upward pull on a rocket jump. `0` = straight away from the blast |
| `[RocketJump]` | `AirControl` | `10` | How fast WASD steers and brakes in the air, m/s² |
| `[RocketJump]` | `Gravity` | `10` | Gravity during a rocket jump, m/s². Lower = floatier |
| `[Targets]` | `Scouts` | `true` | Injure and fling other scouts (Standard only; RocketJump never hurts scouts) |
| `[Targets]` | `Zombies` | `true` | Knock out zombies |
| `[Targets]` | `Scoutmaster` | `true` | Knock down Scoutmaster Myres; after `ScoutmasterHits` rockets he is gone |
| `[Targets]` | `ScoutmasterHits` | `3` | Rockets it takes to get rid of the Scoutmaster (1–10) |
| `[Targets]` | `Spiders` | `true` | Kill spiders (they drop whoever they were holding) |
| `[Targets]` | `Beetles` | `true` | Kill beetles |
| `[Targets]` | `Scorpions` | `true` | Kill scorpions |
| `[Targets]` | `Frogs` | `true` | Kill Gloom frogs (they drop cooked frog legs) |
| `[Targets]` | `Condors` | `true` | Kill Caldera condors (they drop whoever they were carrying) |
| `[Targets]` | `Antlions` | `true` | Kill Mesa antlions |
| `[Targets]` | `Ticks` | `true` | Knock ticks off scouts |
| `[Targets]` | `Jellyfish` | `true` | Smash Shore jellyfish |
| `[Targets]` | `BeeSwarms` | `true` | Scatter bee swarms |
| `[Targets]` | `Beehives` | `true` | Smash beehives (honeycomb drops, bees get angry) |
| `[Targets]` | `Mandrakes` | `true` | Destroy mandrakes |
| `[Targets]` | `Spores` | `true` | Break spore bombs and clear spore clouds |
| `[Targets]` | `Dynamite` | `true` | Set off dynamite |
| `[Targets]` | `Coconuts` | `true` | Knock coconuts off palm trees |
| `[Targets]` | `Tumbleweeds` | `true` | Push Mesa tumbleweeds |
| `[Targets]` | `BingBong` | `true` | Make Bing Bong squeak and fly |
| `[Targets]` | `Balloons` | `true` | Pop balloons |

In multiplayer, `Mode`, `Shots` and everything under `[Targets]` come from the host, so the whole lobby plays by the same rules. `Knockback` is the shooter's: it travels with each hit. Everything else stays per player.

The host keeps track of which suitcases gave a bazooka in each run, in `BepInEx/config/PeakBazooka.loot.json`, so the per-biome cap and the Roots picks survive loading a quicksave or the host leaving mid-run.

## Translations

The bazooka's name follows PEAK's language setting (all 15 languages). To change it, create `BepInEx/config/PeakBazooka.localization.csv` with a `Key` column and one column per language, for example:

```csv
Key,English,German
NAME_BAZOOKA,BAZOOKA,PANZERFAUST
```

The key is `NAME_BAZOOKA`. Language columns are `English`, `French`, `Italian`, `German`, `SpanishSpain`, `SpanishLatam`, `BRPortuguese`, `Russian`, `Ukrainian`, `SimplifiedChinese`, `TraditionalChinese`, `Japanese`, `Korean`, `Polish` and `Turkish`. Empty cells keep the built-in text.

## Install

1. Install the package with r2modman or Gale.
2. Launch PEAK from the mod manager. Starting the game from Steam skips BepInEx.

## How to test

1. Set `GiveBazookaOnSpawn = true` under `[Bazooka]` (the config appears after one launch) and host a run. On the Shore you start with a bazooka in your hand. Without it, open Roots suitcases to find the two guaranteed bazookas.
2. Press primary fire. A rocket flies out about every 0.8 seconds and explodes where it lands; nearby items get knocked around and cooked.
3. Shoot near another scout in Standard mode. They take Injury and fly. Shoot the floor at your own feet: you fly too, and lose a quarter of your health.
4. Set `Mode = RocketJump` while playing (the change applies on the next shot). Shoot the floor: you launch and steer with WASD. Fire at the ground again while falling to chain jumps. Nobody takes damage.
5. In the Roots (Peak ascent or higher for natural zombies), shoot a zombie: one rocket knocks it out for good.

## Reporting issues

Report bugs and suggestions on [GitHub Issues](https://github.com/fake-game-developers/peak-bazooka/issues). Please include:

- What happened and what you expected
- Whether you were the host or a client, which `Mode`, and the biome
- Your mod version and `BepInEx/LogOutput.log` (r2modman: Settings → Debugging → Copy log file contents to clipboard)

## Credits

- Original author: **Arman Ossi Loko**
- This mod belongs to **Fake Game Developers**
- Based on [Peak_AKGun](https://github.com/TheCodinPro/Peak_AKGun) by TheCodinPro

### Contributors

- [bekto](https://github.com/bekto)

Work based on this mod must credit Arman Ossi Loko and Fake Game Developers. See the [LICENSE](https://github.com/fake-game-developers/peak-bazooka/blob/master/LICENSE).
