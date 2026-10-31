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

Click-to-fire: `item.usingTimePrimary = 0`, `Action_Gun.OnPressed`, Harmony skip of `Action_Antizooka.PlayChargeFX`. On press, `Action_Gun` asks the host to authorize the shot (fire rate, ammo, still held), then every client spawns a `BazookaRocket`. Everything after the trigger — rocket launch, explosion, blast, every hit RPC — goes through the shooter's `BazookaShooter` (added to every `Character` next to `GunCharacterLaunch`, RPCs on the scout's view), and accepted shots live in the static `ShotLedger`. Never route it through the gun: PEAK destroys a held item when it is put away, which used to delete rockets in flight. The shooter's rocket reports impact (`BazookaRocket` sweeps with `SphereCastNonAlloc` and takes the nearest hit that is not the shooter, including colliders it starts inside — a single `SphereCast` stopped at your own arm and flew through point-blank zombies); `DetonateRocket` plays a dynamite explosion scaled so the fireball matches `ExplosionRadius` (default 1.5m = 0.3 of dynamite's 5m; `ExplosionVolume` live cfg) and runs `ShotgunCombat` in that same overlap sphere. The explosion is a visual-only copy of dynamite's prefab (`Plugin.BuildVisualOnlyExplosion`): its `AOE` (dynamite-sized knockback, ragdoll, Injury and item launch in dynamite's own range — the old "nuke") plus colliders and rigidbodies are stripped, and every particle lifetime (also in a copy of `ExplosionEffect.explosionOrb`, kept under an inactive holder) is capped at `SmokeSeconds` (1.5s) — dynamite's ~5s smoke lingered as a dark smudge next to a small fireball. `PlayExplosion` also destroys the instance after `ExplosionCleanupSeconds` (4s). All gameplay comes from `BazookaShooter.ApplyBlast`, out to `Plugin.BlastReach` (`ExplosionRadius`, but at least `MinBlastReach` = 4m — a 1.5m hit sphere made near misses do nothing): one entry per victim (by character / Photon view, not `transform.root` — terrain shares a scene root) at its nearest collider, and `strength` = 1 − distance / reach scales scout/zombie knockback (`GunCharacterLaunch.Blast`: one 6–15 m/s velocity change on every bone right after `Fall()` — a push spread over several steps is eaten by ground friction) and scout Injury (0.4, half at the edge). `PlayExplosion` runs on every client and nudges the ground items that client owns (`PushLooseItems`), as dynamite's `AOE` did. `ExplosionVisualScale` and `ExplosionOneShot` are retired. Self-knockback distance is the nearest ragdoll collider, not `Center`, so a floor shot still launches.

Targets: `[Targets]` has one bool per `ShootableRule` (all on) plus `ScoutmasterHits` (3), host-synced as the `PeakBazooka_Shootables` room bitmask. One rocket = one hit; everything has 1 hit to defeat except the Scoutmaster (earlier rockets call `ShotTarget.PartialHit` → knock-down; the last warps him away with a huge `chillForSeconds`). Zombies are knocked out on the first rocket. Spiders have no vanilla death: `SpiderTarget` lets go, unregisters from `SpiderManager` and deactivates on every client (`BroadcastsDefeat`), and only counts a blast near the spider's body because its trigger is a tall column. `ShotgunCombat.BlastPoint` is the blast centre for that check.

The shooter is not a `ShotgunCombat` scout hit: `ApplySelfBlast` handles them. `Mode` (host-synced, `BazookaMode`) picks the rules. **Standard**: other scouts get Injury + ragdoll; self-blast is `Fall()` + `SelfKnockback` (distance falloff, extra up) + `SelfDamage` (0.25, same full-to-half curve as `ScoutInjury`). **TF2**: scouts are never hit (shooter skips them in `TryHitCharacter`, victim re-checks in `ApplyGunImpact`); creatures still are; no recoil; self-blast is `RocketJump` → `GunCharacterLaunch.BeginRocketFlight`. Old `TF2RocketJump = true` / `SelfRagdoll = false` migrate to `Mode = TF2`.

Rocket flight (`RocketFlight.cs`): PEAK multiplies each bodypart's velocity by `CharacterMovement.drag` (0.85) every physics step while you have ragdoll control, which kills a launch in ~0.1s. While flying, Harmony prefixes on `Bodypart.Drag` / `CharacterItems.AddDrag` keep the shared velocity (`data.avarageVelocity`, capped at 40 m/s) and damp only each rig's motion relative to it; `Bodypart.AddMovementForce` / `CharacterItems.AddMovementForce` become TF2 air acceleration (capped wish speed); `CharacterMovement.GetGravityForce` returns `[TF2] Gravity`. Rigs are `ContinuousDynamic` while flying. Flight ends the moment you are grounded, or on climbing, being carried, ragdoll/pass-out, or 15s. Launch sets `sinceJump = 0` once (like a vanilla jump: `CanStand` needs `sinceJump` > 0.3s, so the floor you blasted off is not a landing); each step holds `sinceGrounded` at ≤ 0.21s (no fall score — `Land` only checks after 0.5s — and no mid-air jump) plus `CapFallDamage(0)`. Never hold `sinceJump` at 0 every step: then `isGrounded` never comes back and you slide around in air physics after touching down. Do not go back to fighting drag with `sinceGrounded` / `isJumping` / forced ragdoll control — that was the old broken TF2 jump. `Plugin.Update` polls `PeakBazooka.cfg` every 0.5s and `Config.Reload()`s — do not cache config at item-clone time.

`Action_Antizooka.RPCA_Shoot` still plays muzzle flash, fire SFX, and recoil juice after the host accepts the shot.

Magazine is `Action_Ammo`. `Shots` default 0 = infinite (`totalUses = -1`). Host-synced. No ammo piles / refill.

## Do not

- Reintroduce a custom mesh / `HoldPose` config / hand-anchor solver unless the user asks for a new model
- `Destroy` ParticleSystems on the AntiZooka clone (muzzle flash lives there)
- `PhotonNetwork.Instantiate` the AntiSphere prefab from the clone
- Instantiate dynamite's raw `explosionPrefab` — its `AOE` hits everyone in dynamite range; use `Plugin.ExplosionPrefab` (visual only)
- Leave `Antigrav` on the clone (that is why a dropped AntiZooka floats)
