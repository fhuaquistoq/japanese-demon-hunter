# Monsters

Model-independent enemy runtime for the survival phase. The gameplay is no longer about kanji,
Japanese vocabulary or voice recognition: small monsters now cling to the moving cart, add load,
slow it down and can be cut off with a sword while a giant zombie chases from behind.

## Runtime components

Core (shared by every small monster, never duplicated per enemy type):

- `MonsterBase` – finite state machine: Spawn, Patrol, SelectTarget, Chase, Approach, Attack,
  Retreat, SelectAttachment, ChaseAttachment, Attach, Attached, Dead.
- `MonsterMovement` + `GroundMonsterMovement` / `FlyingMonsterMovement` – the only difference
  between walking and flying enemies.
- `MonsterTargetSelector` / `MonsterTargetRegistry` / `IMonsterTarget` – the Phase 2 target
  plumbing is kept for the legacy candle behaviour and reused by the cart attack.

Cart attachment:

- `CartAttachmentPoints` – registry of every attachment point on the cart.
- `MonsterAttachmentPoint` – a single configurable slot (accepted kind, capacity, facing).
- `MonsterAttachment` – reserves a slot, moves in, parents itself to the slot, registers load,
  and releases everything when it detaches, dies, is disabled or destroyed.
- `AttachedMonsterAttack` – an attached monster threatens the hunter on a cooldown.

Load and speed:

- `CartMonsterLoad` – the single owner of the load accounting. It keys registrations by
  `MonsterAttachment` instance so a monster can never add its load twice, and never lets the
  total go negative.
  `speedMultiplier = Clamp(1 - totalLoad / maxLoad, minimumMultiplier, 1)`.
- `ICartSpeedPenaltyReceiver` (Prototype assembly) – how the load reaches movement. The provisional
  cart implements it through `SimulatedCartSurvivalController`; a production cart can implement the
  same interface without referencing monster code.
- `ICartAccelerationRequester` – the stable hook for the future horse controller (`RequestAcceleration`).

Combat:

- `SwordDamage` – attack windows, continuous sweep (`Physics.OverlapCapsule`) and one hit per
  enemy per window. Swing speed is measured relative to a reference transform so a sword carried
  by a moving cart is not permanently "swinging".
- `MonsterDamageable` – health and death for all five small types through one path.
- `PrototypeSwordController` – desktop-only driver; a future XR interactor calls the same API.

Giant pursuer:

- `GiantZombieController` – always-chasing ground follower with ground snapping, obstacle probing
  and a configurable reach test against the cart rear. It never attaches and never adds load.
- `GiantZombieSpawner` – spawns exactly one giant behind the cart using the cart's current position
  and facing.

Spawning:

- `MonsterSpawner` – one weighted 360-degree generator for all five small types, with a hard
  active-enemy cap and distance-based retirement. Attached monsters are never retired by distance.
- `MonsterSpawnEntry` – per type: prefab, spawn weight, movement type, min/max radius, flying
  height range and the load it contributes.

Prototype helpers (Prototype assembly):

- `SimulatedCartMovement` / `SimulatedCartSurvivalController` – provisional cart and speed.
- `SimulatedHunterMovement` – desktop-only locomotion inside the cart bounds. It never moves a
  camera or an XR Origin.
- `PrototypeHunterMonsterTarget` / `PrototypeCandleMonsterTarget` – Phase 1 adapters.
- `SurvivalPrototypeHud` – on-screen speed, load, multiplier and giant distance.

## Editor tools

`Tools > Monsters`:

- `Create Prototype Scene` / `Validate Prototype Scene` (Phase 1).
- `Setup Enemy System` / `Validate Enemy System` / `Inspect Source Models` (Phase 2).
- `Setup Survival Systems` – builds the six monster prefabs, adds the attachment points, the load
  registry, the sword, the hunter prototype movement, the giant spawner and the HUD, then validates
  the scene. Idempotent: it reuses existing prefabs and components and never duplicates them.
- `Validate Survival Systems`, `Run Phase 3 Deterministic Verification`,
  `Run Phase 3 Play Mode Smoke Test`.

Batch entry points (Unity `-batchmode -nographics`):

| Flag | Purpose |
| --- | --- |
| `-survivalSetup` | Runs the whole setup and validates it. |
| `-survivalVerify` | Runs the deterministic Phase 3 verification. |
| `-survivalPlaySmoke` | Runs the Phase 3 Play Mode smoke test. |

## Not included

No voice recognition, kanji, Japanese pronunciation, educational mechanics, victory/defeat rules,
full VR rig or full horse controller. Those remain integration points owned by other teammates.
