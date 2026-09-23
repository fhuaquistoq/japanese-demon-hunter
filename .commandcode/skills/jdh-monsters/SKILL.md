---
name: jdh-monsters
description: Crea o extiende monstruos de Japanese Demon Hunter — FSM MonsterBase, spawner por entradas, movimiento terrestre/volador, colgarse de la carreta con carga, daño por barrido y el monstruo gigante. Úsalo cuando se añadan tipos de monstruo, comportamientos, ataques, spawn, o el trigger de derrota del gigante.
---

# jdh-monsters

Sistema `JapaneseDemonHunter.Monsters`. Contexto en [`AGENTS.md`](../../AGENTS.md) §6.2. Principio del proyecto: **un sistema base configurable por tipo** — nunca copies scripts por variante.

## Receta: tipo de monstruo nuevo (checklist)

1. **Arte primero** (`Assets/Art/Monsters/<Tipo>/`): modelo `.fbx` + `Animations/*.controller` + `Prefabs/*.prefab`. Ejecuta `Tools/Monsters/Inspect Source Models` y **usa los nombres de estado/clip que realmente existan** — no asumas nombres.
2. **Prefab** con estos componentes (`[DisallowMultipleComponent]`, todos con `Configure(...)` público para setup por código y tests):
   - `MonsterBase` (FSM + radii/tiempos por Inspector)
   - `GroundMonsterMovement` **o** `FlyingMonsterMovement`
   - `MonsterTargetSelector` (+ `strategy`)
   - `MonsterAttack` y `MonsterAnimationController` (estados `Locomotion`/`Attack`/`Death` **configurables por string**)
   - `MonsterDamageable` (vida + `vulnerableToNormalSword`)
   - Si se cuelga de la carreta: `MonsterAttachment` (+ `AttachedMonsterAttack` si ataca al jugador colgado)
3. **Spawn**: añade un `MonsterSpawnEntry` en el `MonsterSpawner` existente (prefab, `movementType`, `weight`, radios, alturas de vuelo, `attachmentLoad`). Un único spawner cubre todos los tipos; no crees spawners nuevos.
4. **Suelo caminable**: el movimiento terrestre solo pisa geometría marcada con **`MonsterGroundSurface`** (así la carreta en movimiento no se vuelve suelo). Marca el terreno del mapa con ese componente.
5. Balance (vida, daño, radios, cooldowns, `loadContribution`) siempre por Inspector, nunca hardcodeado.

## FSM (`MonsterState`)

`Spawn → Patrol → SelectTarget → Chase → Approach → Attack → …` y la rama de enganche `SelectAttachment → ChaseAttachment → Attach → Attached`, más `Retreat` (si supera `maximumChaseDistance` del centro) y `Dead` (`Kill()` dispara `Died`, `Retire()` desactiva y despide `BecameInactive` para que el spawner destruya la instancia). Añade estados solo si el flujo de diseño no cabe; en general basta con tunear radios/tiempos (`detectionRadius`, `attackDistance`, `patrolRadius`, `retreatTimeout`, `deathDisableDelay`).

## Colgarse de la carreta y penalizar velocidad

`MonsterAttachment.TrySelectPoint()` reserva un `MonsterAttachmentPoint` (capacidad + `Ground`/`Flying`) vía `CartAttachmentPoints` (el más cercano primero). Al engancharse (`TickAttach`): se re-parenta al punto, registra su `loadContribution` en `CartMonsterLoad`, que empuja `SpeedMultiplier = clamp(1 − totalLoad/referenceMaximumLoad, min, 1)` a **`ICartSpeedPenaltyReceiver`** (frontera con carreta, en `Prototype`). Al soltar (`Detach`) se descarga. Los eventos `Attached`/`Detached`/`LoadChanged` son el lugar para VFX/SFX.

Diseño del juego: los monstruos colgados **ralentizan la carreta**; el jugador los **golpea para bajarlos**. El puente con la carreta VR real aún no está conectado (ver `jdh-reins-gestures` §Integración) — no lo fuerces desde aquí.

## Daño: bate y puños (usar `SwordDamage`)

- `SwordDamage` hace daño por **barrido de cápsula** del recorrido de la punta con **ventanas activadas por velocidad** relativa a `velocityReference` (configúralo con el transform de la carreta/raíz: sin eso, el movimiento de la carreta cuenta como golpe). Un solo hit por monstruo por ventana.
- Para arma nueva (palo/bate) o puños: reutiliza `SwordDamage` con `damage`/`sweepRadius` distintos. Referencia de diseño: **bate = 2–3 golpes**, **puños = 5–7 golpes** para botarlos → calibra `damage ≈ vida/2…vida/3` (bate) y `damage ≈ vida/5…vida/7` (puños) y verifícalo con `MonsterDamageable.MaximumHealth`. El driver desktop de ejemplo es `PrototypeSwordController`; el interactor XR real debe abrir/cerrar ventanas con `BeginAttackWindow()`/`EndAttackWindow()` o dejar que la velocidad las active (ver `jdh-xr-hand-interactions`).

## Objetivos (a quién atacan)

`IMonsterTarget` (Hunter/Candle, `TryReceiveHit`, `AvailabilityChanged`) registrado en `MonsterTargetRegistry`; la elección la hace `MonsterTargetSelector` (`Closest`, `ClosestLitCandle`, `RandomLitCandle`, `PrioritizeHunter`). Para que algo nuevo sea atacable: implementa `IMonsterTarget` y regístralo — no toques la FSM.

## Gigante (derrota)

`GiantZombieSpawner` lanza el `GiantZombie.prefab` **detrás de la carreta** sobre suelo `MonsterGroundSurface`; `GiantZombieController` persigue `cartRearReachPoint` y emite **`GiantCaughtCart`** al quedar a < `catchRange`. Pendiente de diseño (ver AGENTS.md §9): el trigger "aparece cuando la velocidad cae/se detiene" y el **tinte rojo oscuro de derrota** — engancha ambos a `GiantCaughtCart`/al observador de velocidad (evento `EffectiveSpeedChanged` del prototipo), sin mover la cámara.

## Verificación (obligatoria)

1. `Tools/Monsters/Validate Enemy System` y `Validate Survival Systems` tras cambios de setup.
2. `Tools/Monsters/Run Phase 2 Deterministic Verification` (transiciones de FSM, ataques, spawn) y `Run Phase 3 Deterministic Verification` (carga de la carreta, gigante, HUD).
3. Los dos **Play Mode Smoke Test** (fase 2 y 3) para comportamiento en escena.
4. Reporta solo lo ejecutado; el resto marca como pendiente de validación en Quest real (`jdh-dev-loop` §5).
