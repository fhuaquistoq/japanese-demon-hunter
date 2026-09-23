---
name: jdh-reins-gestures
description: Implementa o ajusta el sistema de riendas de Japanese Demon Hunter — soga con física Verlet, gestos de latigazo/frenado/cambio de carril, camino de 3 carriles con obstáculos y motor de la carreta. Úsalo cuando se toque la soga, las manos que la sostienen, los gestos, los carriles, los obstáculos o el movimiento de la carreta.
---

# jdh-reins-gestures

Sistema `Reins` (namespace `Reins`, ensamblado `Reins` → `Oculus.Interaction`). Contexto en [`AGENTS.md`](../../AGENTS.md) §6.1. Regla VR: este sistema mueve la **carreta**; jamás muevas cámara ni XR Origin.

## Arquitectura (leer antes de cambiar nada)

```
ReinHandle (×2, una por mano)                ClosedReinLoop
  HandGrabInteractable + IHand                 LineRenderer (loop 32 pts)
  pull = localPos - restLocalPosition          RopePhysics (Verlet, 4 pins)
  ReinGestureStateMachine ──┐                  pins: [cabeza caballo izq,
                            ▼                         empuñadura izq,
CarriageMotor.Update ──► ReinCommandArbitration.Select      empuñadura der,
  apply speed / lane / brake                   cabeza caballo der]
  LaneTransitionModel (smoothstep)
  ForestRoad.TryStopOnRock ──► CarriageStopModel (trabe)
```

- `RopePhysics.cs`: simulación **pura y sin allocs** (arrays que aporta `ClosedReinLoop`). No metas lógica de gameplay ahí.
- `ReinDrivingModel.cs`: todos los modelos puros testeables (máquina de gestos, arbitraje, carriles, obstáculos). Cualquier lógica nueva va aquí primero, con test.
- `CarriageMotor.cs`: único punto que mueve la carreta (−Z + transición X de carril). Expone `Speed` y `Lane`.

## Pipeline de gestos (umbrales en `ReinHandle`, por Inspector)

| Gesto | Condición sobre `pull` (m respecto a `restLocalPosition`) | Resultado |
|---|---|---|
| Latigazo (acelerar) | lift `y ≥ 0.18` y luego caída `≥ 0.16` con velocidad `≥ 0.45` m/s dentro de `0.8` s | `Accelerate` (+`accelerationPerStroke`, default 0.7) |
| Frenar | `pull.z ≥ 0.22` | `Brake` (−0.65 por jalón) |
| Cambio de carril | `|pull.x| ≥ 0.24` | `LanePull(dir)` — `ThreeLaneModel` avanza ±1 carril dentro de −1..1 |
| Trabe por borde | LanePull hacia el límite del camino | penalización `boundarySpeedPenalty` (0.7) |

- Re-arm: el gesto vuelve a estar disponible al regresar a `0.12` m del reposo; cooldown `0.55` s entre gestos.
- **Arbitraje** (`ReinCommandArbitration`): Brake > LanePull > Accelerate; empate → rienda izquierda. Máximo un comando por frame.
- Solo conduce la **mano esperada** (`Handedness`), seleccionada (`SelectingInteractors`) y con tracking válido (`ReinHandOwnership.CanDrive`). Al soltar, la empuñadura vuelve al reposo (`returnSpeed`).
- Cambio de carril suave: `LaneTransitionModel` interpola con smoothstep (`laneWidth` 2.8, `laneShiftDuration` 0.8). Si pides un cambio mientras transiciona, arranca desde la X interpolada real (hay test: `MidShiftRequestStartsFromActualInterpolatedPosition`).

## Obstáculos y trabe

- `ForestRoad` recicla tiles y activa piedras según `ObstacleSchedule.BlockedLaneMask(group)`: patrón determinista de 6 grupos que bloquea 1–2 carriles y **siempre deja 1 libre** (tests `ForestRoadTests`).
- La colisión es **barrida analítica** (`ObstacleCollisionModel.TrySweep`, con X interpolada al cruzar la piedra), no física de Unity — no añadas Colliders a las piedras.
- Al chocar: `CarriageStopModel.Stop()` (velocidad 0, latched). Un solo `Accelerate` limpia el latch y empuja (`OneAccelerationClearsStoppedLatchAndAddsSpeed`).

## Cómo añadir un gesto nuevo

1. Añade el kind en `ReinGestureKind` y la detección en `ReinGestureStateMachine.Step` (con re-arm/cooldown igual que los demás).
2. Asigna prioridad en `ReinCommandArbitration.Priority`.
3. Maneja el caso en `CarriageMotor.Apply` (todo valor de balance como `[SerializeField]`).
4. Tests primero en `Assets/Tests/EditMode/ReinDrivingModelTests.cs` (máquina pura, sin Play Mode).
5. Ejecuta `/jdh-dev-loop` §3 (al menos `run_tests` EditMode).

## Integración con monstruos (pendiente, no lo inventes)

`CarriageMotor` **todavía no implementa** `ICartSpeedPenaltyReceiver` (declarada en `Prototype/CartSpeedIntegration.cs`), por lo que los monstruos colgados aún no frenan la carreta VR. Para cerrar esa integración: implementar la interfaz en un adaptador junto al motor (o en `CarriageMotor` mismo) que traduzca `SetMonsterLoadMultiplier` a un tope de velocidad, sin referenciar `JapaneseDemonHunter.Monsters`. `ICartAccelerationRequester.RequestAcceleration()` es el espejo para pedir aceleración (latigazo). Verificar con tests EditMode + smoke de Fase 3.

## Verificación

- `mcp__unity__run_tests {mode:"EditMode"}`: `Reins.EditModeTests` cubre física de soga, gestos, arbitraje, carriles y barrido de obstáculos.
- Para comportamiento de escena: `/jdh-dev-loop` §4 (el XR Simulator no valida umbrales de gestos finos — declararlo siempre).
