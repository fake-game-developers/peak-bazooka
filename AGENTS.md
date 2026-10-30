# Agent notes

Player-facing setup is in [README.md](README.md).

The solution is `Peak.Shotgun.slnx`. The C# namespace is still `Peak.Shotgun`. The assembly and BepInEx plugin id are `PeakBazooka`. The Thunderstore package name is `Bazooka`.

The gun is a clone of PEAK's AntiZooka. `Plugin.CreateFromAntiZooka` waits for the item catalog, clones that prefab, strips `Action_RaycastSpawnSomething` (the antigravity bubble), `Action_ReduceUses`, and `Antigrav` (so a dropped bazooka falls instead of hovering), keeps `Action_Antizooka` for charge/muzzle/shell juice, and registers the clone with PEAKLib. Do not replace the vanilla AntiZooka.

Vanilla hold pose, hands, mesh, and shaders stay. There is no custom arm IK or mesh swap.

## Where things live

| Path | What it is |
|---|---|
| `src/PeakShotgun/` | Gameplay code |
| `src/PeakShotgun/Targets/` | Shootable rules and one `ShotTarget` per world target |
| `src/PeakShotgun/icons/BazookaIcon.png` | Hotbar icon (vanilla AntiZooka texture) |
| `antizooka/` | Vanilla AntiZooka dump + concept art |

Embedded resource names in `Peak.Shotgun.csproj` must match `GetManifestResourceStream` strings.

## Fire

Click-to-fire: `item.usingTimePrimary = 0`, `Action_Gun.OnPressed`, Harmony skip of `Action_Antizooka.PlayChargeFX`. On press, `Action_Gun` asks the host to authorize the shot, then every client spawns a `BazookaRocket`. The shooter's rocket reports impact; `DetonateRocket` plays a dynamite explosion scaled so the fireball matches `ExplosionRadius` (default 1.5m = 0.3 of dynamite's 5m; `ExplosionVolume` live cfg) and runs `ShotgunCombat` in that same overlap sphere. Dedup blast victims by character / Photon view, not `transform.root` (terrain shares a scene root). `ExplosionVisualScale` is retired; old `ExplosionRadius = 6` migrates to match the saved visual scale. Self-knockback distance is the nearest ragdoll collider, not `Center`, so a floor shot still launches. `ExplosionOneShot` (default true, host-synced) applies `HitsToDefeatSettings.MaxHits` so one rocket KOs everyone in the blast. There is no `[Shootables]` or `[HitsToDefeat]` cfg — every target type is always on; hit counts are hardcoded (zombies 2, Scoutmaster 4) and only matter when one-shot is off. The shooter is not a `ShotgunCombat` scout hit: `ApplySelfBlast` launches them with `SelfKnockback` (distance falloff, extra up). `TF2RocketJump` (default false, per player, live cfg) is the soldier rocket jump: no `Fall()`, `SelfDamage` is 0, no gun recoil, and `GunCharacterLaunch.BeginAirSteer` keeps ragdoll control and zeros spin so WASD steers. `SelfRagdoll` in code is `!TF2RocketJump`; old cfg `SelfRagdoll = false` migrates to `TF2RocketJump = true`. Do not pin `sinceGrounded` at 0 or set `isJumping` — that is PEAK jump gravity (+10 up) and floats you into the ceiling. Airborne `sinceGrounded >= 1` so `maxGravity` (-20) applies after the impulse. Keep `sinceJump` at 0 and `CapFallDamage(0)` so landing is not treated as a 2s fall (~30% Injury). `Plugin.Update` polls `PeakBazooka.cfg` every 0.5s and `Config.Reload()`s — do not cache config at item-clone time.

`Action_Antizooka.RPCA_Shoot` still plays muzzle flash, fire SFX, and recoil juice after the host accepts the shot.

Magazine is `Action_Ammo`. `Shots` default 8; **0 = infinite** (`totalUses = -1`). Host-synced. No ammo piles / refill.

## Do not

- Reintroduce a custom mesh / `HoldPose` config / hand-anchor solver unless the user asks for a new model
- `Destroy` ParticleSystems on the AntiZooka clone (muzzle flash lives there)
- `PhotonNetwork.Instantiate` the AntiSphere prefab from the clone
- Leave `Antigrav` on the clone (that is why a dropped AntiZooka floats)
