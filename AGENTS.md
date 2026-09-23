# Japanese Demon Hunter — AGENTS.md

> Guía de trabajo para agentes de código. Léela antes de tocar nada en este proyecto.
> Última actualización: 2026-09-23 (inspección del repo + Editor de Unity en vivo).

## 1. El juego (diseño objetivo)

Un ciudadano escapa de monstruos montado en una **carreta de madera tirada por 2 caballos**. El jugador camina libremente sobre la carreta y controla a los caballos **sosteniendo una soga (riendas) con física realista**:

- Levantar la soga con ambas manos y **agitarla hacia abajo** → los caballos galopan (acelerar).
- **Tirar la soga hacia atrás** → frenar.
- **Tirar hacia los lados** → cambio de carril. El camino es de **3 carriles**, **con curvas**; la carreta siempre sigue un carril y el cambio debe ser suave.
- Obstáculos: **montículos de piedra** que detienen la carreta; hay que agitar la soga para liberarla y seguir.
- **Monstruos** persiguen desde atrás: pueden **colgarse de la parte trasera** (arrastrados) y eso **ralentiza la carreta**. El jugador los golpea con un **palo/bate de madera (2–3 golpes)** o con **puños (5–7 golpes)**; si pierde el bate, valen los puños.
- Si la carreta pierde mucha velocidad o se detiene sin avanzar, un **monstruo gigante** ataca: la pantalla se tiñe de **rojo oscuro** → derrota.
- **Victoria**: llegar al final del camino, un **reino con torres de defensa**.
- Ambientación: **bosque de noche**, camino de tierra, visibilidad limitada; **lámparas en la carreta (adelante y atrás)** como iluminación principal + **luz de luna tenue**.

## 2. Reglas de trabajo (obligatorias)

1. **Nunca compilar builds.** El humano compila manualmente para los Quest reales. Prohibido `unity build` / Build Pipeline salvo petición explícita.
2. **Flujo de desarrollo**: Unity CLI (`unity …`) + Editor de Unity en vivo (Unity MCP / `unity command`) + **Meta XR Simulator** para pruebas funcionales.
3. **100% hand tracking** (sin controles Meta): toda interacción es con manos (agarrar, arrojar, agitar, jalar, golpear). `OVRProjectConfig.handTrackingSupport = HandsOnly`.
4. **Regla VR**: jamás mover ni teletransportar la cámara o el XR Origin artificialmente; el jugador nunca sufre desplazamientos forzados (ver docstrings de `SmoothFollowCamera` y `SimulatedHunterMovement`).
5. **Límites de propiedad (trabajo en equipo)**: el **VR rig**, el **movimiento de la carreta**, los **caballos** y los **mapas** pertenecen a compañeros. No los reescribas: **integra por interfaces/eventos** (`ICartSpeedPenaltyReceiver`, `ICartAccelerationRequester`, `IMonsterTarget`, eventos C#). Features obsoletas se desacoplan o deshabilitan, no se borran.
6. **Sin git commits automáticos**; deja los cambios sin commitear para revisión. **No borrar** escenas, prefabs, scripts, modelos ni carpetas (p. ej. `Assets/_Recovery/`, `Assets/XR 1..8`) sin confirmación humana.
7. **Cambio mínimo** para defectos verificados; nada de reescrituras. Todo valor de balance (velocidades, daños, umbrales, cargas, cooldowns) va como **campo `[SerializeField]` expuesto en el Inspector**.
8. **Arquitectura modular**: un sistema base configurable por tipo (una FSM `MonsterBase`, un `MonsterSpawner` con entradas) en vez de lógica duplicada por variante.
9. **No asumas nombres de assets ni de animaciones**: los estados de Animator son strings configurables (`MonsterAnimationController`). Inspecciona los controllers reales (`Tools/Monsters/Inspect Source Models`) y adapta la implementación a lo que exista.
10. El **Editor tooling debe ser idempotente y no destructivo** (sin duplicar componentes ni pisar cambios manuales del Inspector); los menús existentes cumplen esto — sigue ese patrón.
11. Entregables y respuestas **en español**. Al final, informe estructurado: archivos creados/modificados, cómo ejecutar/generar, verificaciones realmente ejecutadas (nunca inventar resultados), puntos de integración y limitaciones conocidas.

## 3. Stack verificado (2026-09-23)

| Componente | Versión / estado |
|---|---|
| Unity | 6000.6.2f1 — Build Profile activo **Meta Quest** (Android) |
| Meta XR SDK | All-in-One 205.0.0 (core, interaction, interaction.ovr, mrutilitykit, haptics, platform; audio 85.0.0, voice 85.0.1) |
| XR | OpenXR 1.18.0 (loader en Android y Standalone), XR Hands 1.9.0 (indirecto), XR Management 4.4.0 (indirecto) |
| Render | URP 17.6.0 — `Assets/Settings/Mobile_RPAsset` (MSAA 4x, render scale 0.8, sombras 50 m) y `PC_RPAsset` |
| Input | Input System 1.20.0 (`activeInputHandler` = Input System) |
| CLI | `unity` CLI 1.0.0-beta.11 + `com.unity.pipeline` 0.7.0-exp.1 |
| Android | ARM64 + IL2CPP, Vulkan, Linear, ASTC, MinSDK 32 / TargetSDK 34, bundle v0.1.0 (application ID aún el del template — ver §4) |

Los paquetes `com.meta.xr.*` se resuelven del registry estándar de UPM (`packages.unity.com`). **No instalar `com.unity.xr.oculus`** (conflicto con OpenXR) ni `com.meta.xr.simulator` (**deprecado**; ver §7).

## 4. Configuración XR / Quest 2

- **Hand tracking**: `Assets/Oculus/OculusProjectConfig.asset` → `handTrackingSupport = HandsOnly (2)`, `handTrackingFrequency = LOW` (para gestos muy rápidos, probar `HIGH`/60 Hz — es un toggle de balance).
  - Tras **cualquier** cambio en `OVRProjectConfig`/`OVRManager`: ejecutar `OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(true)` y **verificar** `Assets/Plugins/Android/AndroidManifest.xml` (`oculus.software.handtracking` requerido + permiso `com.oculus.permission.HAND_TRACKING`).
  - **Nunca editar el AndroidManifest a mano** para features gestionadas por `OVRProjectConfig`.
- **XR Plug-in Management**: `OpenXRLoader` activo en Android y Standalone (`Assets/XR/`). Feature `MetaXRFeature` habilitada; perfiles Oculus Touch habilitados (requerido por OVRInput/UPST). Los features "HandTracking" de Unity OpenXR están off porque el tracking de manos fluye por OVRPlugin (`MetaXRFeature`) + Interaction SDK — no tocar sin evidencia.
- **Application ID (pendiente manual)**: el bundle ID sigue siendo el del template (`com.UnityTechnologies.com.unity.template.urpblank`). La API de PlayerSettings no logra persistir este campo desde MCP/eval (el overload `BuildTargetGroup` está obsoleto y `NamedBuildTarget` no es accesible desde el compilador del eval — verificado en disco). Cambiarlo a mano: **Edit > Project Settings > Player > Other Settings > Identification → Application Identifier: `com.unsa.ihc.japandemonhunter`**, luego **File > Save Project**. Es el mismo campo que marca pendiente el UPST ("Set up the application ID and the package name").
- **UPST** (Meta > Tools > Project Setup Tool): 110 OK / 15 pendientes (2026-09-23). Pendientes: DUC y app ID de *Platform SDK* (ignorar si no se usan APIs de Platform), Application SpaceWarp (opcional), foveated rendering (recomendado para rendimiento, pendiente manual) y features no usadas por el juego (eye/body/face tracking, passthrough, anchors, etc.).
- **Dispositivo objetivo: Meta Quest 2**. En el XR Simulator el perfil por defecto es Quest 3: cambiar **Inputs > Device info > Device → Meta Quest 2** y reiniciar Play Mode.

## 5. Mapa del proyecto

### Ensamblados (`Assets/Scripts/**.asmdef`, `Assets/Tests/**`)

```
Reins ─────────────────► Oculus.Interaction (Meta Interaction SDK)
Reins.EditModeTests ────► Reins, Oculus.Interaction (Editor)
JapaneseDemonHunter.Prototype ────────► Unity.InputSystem
JapaneseDemonHunter.Prototype.Editor ─► Prototype (Editor)
JapaneseDemonHunter.Prototype.Tests ──► Prototype + NUnit (Editor)
JapaneseDemonHunter.Monsters ─────────► Prototype, Unity.InputSystem
JapaneseDemonHunter.Monsters.Editor ──► Monsters, Prototype (Editor)
```

`Reins` y `Monsters` **no se referencian entre sí**; la frontera Monsters → carreta es la interfaz `ICartSpeedPenaltyReceiver` (declarada en `Prototype`).

### Carpetas clave

- `Assets/Scripts/{Reins,Monsters,Prototype}` (+ `Editor/`, `Tests/`) — código por sistema (§6).
- `Assets/Scenes/SampleScene.unity` — **escena VR real (trabajo del equipo, con cambios sin commitear)**: OVRCameraRig/anchors, `VehicleRoot` + `VehicleRoot_CarriageMotor`, `RoadRoot`, `ClosedReinLoop`, `Left/Right_ReinHandle`, `Left/Right_HandGrabInteractable`, `Tether_Left/Right`. No modificar a ciegas.
- `Assets/Scenes/MonstersPrototype.unity` — prototipo desktop generado por `Tools/Monsters/Create Prototype Scene` (regenerable y validable).
- `Assets/Art/Monsters/{Zombie,Spider,GiantZombie,Ghost,Demon,Bat}` — modelos FBX reales + prefabs (`ZombieDemon`, `SpiderMonster`, `GiantZombie`, `GhostMonster`, `DemonMonster`, `BatDemon`) + Animator controllers (packs de Quaternius y otros). *`Bat` = murciélago demonio, no el arma.*
- `Assets/Prefabs/Carriage/CarriagePrototype.prefab`, `Assets/Prefabs/Interaction/RopeProxy.prefab`.
- `Assets/Materials/Prototype/` (`Mat_ReinLeft/Right`, `Mat_RopeProxy`, `Mat_CarriageWood`, `Mat_WheelWood`, `Mat_Hardware`, `Mat_HorsePlaceholder`) y `Assets/Prototype/Materials/` (generadas por el scene creator).
- `Assets/Settings/` — URP assets (Mobile/PC + renderers) y `Build Profiles/Meta Quest.asset`.
- `Assets/XR/` — XR Plug-in Management (OpenXR) + capa `XrApiLayer_METAX_operator` (tooling Meta XR Operator).
- `Assets/Oculus/OculusProjectConfig.asset` — features Meta del proyecto.
- `Assets/_Recovery/` — copias de recuperación de escena (`0.unity`, `0 (1).unity`): **no borrar**.
- `Assets/XR 1 … Assets/XR 8` — carpetas **vacías** residuales: candidatas a limpieza manual, solo con confirmación humana.

## 6. Sistemas

### 6.1 Reins — riendas, carreta y camino (ns `Reins`)

| Archivo | Rol |
|---|---|
| `RopePhysics.cs` | Simulación **Verlet** sin allocs del lazo cerrado de 4 pins (32 puntos): gravedad, suelo (`minimumDeckHeight`), iteraciones de restricción. Puro C# y testeado. |
| `ClosedReinLoop.cs` | Dibuja el lazo con `LineRenderer` (loop, espacio local de `vehicleRoot`). Pins: cabeza caballo izq., empuñadura izq., empuñadura der., cabeza caballo der. |
| `ReinHandle.cs` | Empuñadura por mano: `HandGrabInteractable` (Interaction SDK) + `IHand`. Mide el *pull* respecto a `restLocalPosition`, vuelve al reposo al soltar, expone `ReadGesture(dt)`. |
| `ReinDrivingModel.cs` | Modelos puros: `ReinGestureStateMachine`, `ReinHandOwnership`, `ReinCommandArbitration`, `LaneTransitionModel`, `ObstacleSchedule`, `CarriageStopModel`, `ObstacleCollisionModel`, `ThreeLaneModel`. |
| `CarriageMotor.cs` | Cada `Update` arbitra ambos gestos y mueve la carreta hacia −Z con transición suave de carril; consulta `ForestRoad.TryStopOnRock` (colisión barrida) → se traba hasta acelerar de nuevo. Expone `Speed` y `Lane`. |
| `ForestRoad.cs` | Camino infinito por reciclaje de tiles (primitivas + materiales runtime): 3 carriles de 2.8 m, bermas, divisores, árboles y **piedras de obstáculo** por patrón determinista (`ObstacleSchedule`; siempre queda ≥1 carril libre). |

**Gestos** (umbrales por Inspector en `ReinHandle`): lift ≥ 0.18 m y luego caída ≥ 0.16 m con velocidad ≥ 0.45 m/s dentro de 0.8 s ⇒ **Accelerate** (latigazo); `pull.z ≥ 0.22` ⇒ **Brake**; `|pull.x| ≥ 0.24` ⇒ **LanePull** (±1 carril, nunca sale de los 3). Re-arm al volver a 0.12 m del reposo; cooldown 0.55 s. Arbitraje: Brake > LanePull > Accelerate (empate → rienda izquierda). Solo conduce la mano esperada, seleccionada y con tracking válido (`ReinHandOwnership`).

**Integración pendiente**: `CarriageMotor` aún **no implementa** `ICartSpeedPenaltyReceiver` → la carga de monstruos todavía no frena la carreta VR (en el prototipo desktop sí: `SimulatedCartSurvivalController`).

### 6.2 Monsters — enemigos (ns `JapaneseDemonHunter.Monsters`)

- **`MonsterBase`**: FSM de 12 estados (`Spawn, Patrol, SelectTarget, Chase, Approach, Attack, Retreat, SelectAttachment, ChaseAttachment, Attach, Attached, Dead`) con radios/tiempos por Inspector; eventos `StateChanged`, `BecameInactive`, `Died`; `Kill()` y `Retire()`. Se inicializa con `MonsterSpawnContext`.
- **Movimiento**: `MonsterMovement` (abstracto: patrol/chase/approach + rotación) → `GroundMonsterMovement` (solo camina sobre superficies marcadas con **`MonsterGroundSurface`**, paso máx. 0.35 m — evita subir a la carreta —, esquiva obstáculos ±55°) y `FlyingMonsterMovement` (alturas 1.5–7 m, esquiva y trepa).
- **Spawn**: `MonsterSpawner` con `MonsterSpawnEntry` (prefab, tipo, peso, radios, alturas de vuelo, `attachmentLoad`): uno de cada al iniciar, por intervalo, máximo de activos, colocación sobre `MonsterGroundSurface` con holgura y distancia mínima al jugador, retiro a >90 m. Un **único** spawner cubre todos los tipos.
- **Combate**: `MonsterAttack` (alcance, `impactDelay`, cooldown, confirmación por `OverlapSphere` + línea de visión), `AttachedMonsterAttack` (ataca al jugador **mientras está colgado**, repetible), `MonsterDamageable` (vida + `ApplyDamage(amount, source)`, eventos `Damaged/Killed`), `SwordDamage` (daño por **barrido de cápsula** del recorrido de la punta; ventanas activadas por velocidad relativa a `velocityReference` para que el movimiento de la carreta no cuente como golpe; un solo hit por monstruo por ventana). El bate y los puños del juego real deben usar esta API (ver `PrototypeSwordController` como driver desktop).
- **Colgarse de la carreta**: `MonsterAttachment` reserva un `MonsterAttachmentPoint` (capacidad + tipos `Ground/Flying`) vía `CartAttachmentPoints`; al engancharse registra su carga en **`CartMonsterLoad`** ⇒ `SpeedMultiplier` hacia `ICartSpeedPenaltyReceiver` (evento `LoadChanged`). Balance: `loadContribution` por monstruo (def. 8) contra `referenceMaximumLoad` (60), multiplicador mínimo 0.3.
- **Gigante**: `GiantZombieSpawner` (lo lanza **detrás de la carreta** a 22 m sobre suelo marcado) + `GiantZombieController` (persigue `cartRearReachPoint` a 3.6 m/s, esquiva ±50°, evento **`GiantCaughtCart`** al acercarse a <1.8 m). *Falta el trigger "aparece cuando la velocidad cae" y el tinte rojo de derrota (§9).*
- **Objetivos**: `IMonsterTarget` (`Hunter`/`Candle` + `TryReceiveHit`) en `MonsterTargetRegistry`, elegidos por `MonsterTargetSelector` (estrategias `Closest`, `ClosestLitCandle`, `RandomLitCandle`, `PrioritizeHunter`). Implementaciones: `PrototypeHunterMonsterTarget` (evento `SimulatedHit`) y `PrototypeCandleMonsterTarget` (apaga `PrototypeCandle`).
- **Animación**: `MonsterAnimationController` — crossfades a estados **por nombre configurable** (`Locomotion`/`Attack`/`Death` por defecto); `HasAttackAnimation`/`HasDeathAnimation` permiten adaptarse a lo que realmente exista en cada controller.

### 6.3 Prototype — prototipo desktop (ns `JapaneseDemonHunter.Prototype*`)

Simulación sin headset para validar gameplay: `SimulatedCartMovement` (movimiento recto temporal), `SimulatedCartSurvivalController` (**implementa `ICartSpeedPenaltyReceiver` + `ICartAccelerationRequester`**; LShift = latigazo de escritorio), `SimulatedHunterMovement` (WASD local; nunca mueve cámara/XR Origin), `SmoothFollowCamera` (mouse-look desktop), `PrototypeCandle` (vela + evento `Extinguished`), `PrototypeSceneReferences` (superficie de integración de la escena), `SurvivalPrototypeHud` (OnGUI). Contratos compartidos en `CartSpeedIntegration.cs`: `ICartSpeedPenaltyReceiver`, `ICartAccelerationRequester`.

## 7. Flujo de desarrollo diario

1. **Editor vivo**: `unity status` (esperar `state: ready`) y trabajar con `unity command …` / Unity MCP sobre la escena abierta. **Nunca** editar YAML de `.unity`/`.prefab`/`.asset` con el Editor abierto.
2. **Recompilar y verificar** tras cada cambio de código:
   - `unity command recompile` (o MCP `recompile`) → `recompile_status` → `console` sin errores.
   - Tests EditMode: MCP `run_tests {mode: "EditMode"}` (o `unity test <proyecto> --mode EditMode`). Cubren `Reins.EditModeTests` (`RopePhysicsTests`, `ReinDrivingModelTests`, `ForestRoadTests`) y `JapaneseDemonHunter.Prototype.Tests`.
   - Verificaciones deterministas de monstruos: menús `Tools/Monsters/*` (§8).
3. **Probar en Meta XR Simulator** (funcional; **no** rendimiento):
   - Es una **app standalone** de escritorio (runtime OpenXR): descargar desde MQDH → Tools o `developers.meta.com/horizon/downloads/package/meta-xr-simulator-windows/`.
   - **No requiere paquete Unity** (`com.meta.xr.simulator` está deprecado y no está instalado). Activar desde Unity: **Window > Meta > Meta XR Simulator > Activate** (o el icono junto a Play); la consola debe decir `[Meta XR Simulator is activated]`. `Deactivate` vuelve al headset; `Status` consulta el estado.
   - Elegir **Device = Meta Quest 2** (Inputs > Device info) y reiniciar Play Mode al cambiarlo. Deja el simulador activo entre corridas.
   - **Límite**: las manos simuladas son **poses discretas por teclado**, no tracking continuo: no valida umbrales de agarre ni calidad de gestos finos. La validación real de riendas/agarres se hace en el Quest (el humano compila e instala).
4. **Nunca compilar** (`unity build`, Build Pipeline, etc.). El humano hace los builds para Quest.

## 8. Editor tooling (menús `Tools/Monsters/*`)

| Menú | Qué hace | Entrada batch |
|---|---|---|
| Create Prototype Scene | Genera `MonstersPrototype.unity` (idempotente: si existe, solo la abre) | `MonstersPrototypeSceneCreator.CreatePrototypeSceneFromCommandLine` |
| Validate Prototype Scene | Valida configuración y comportamiento determinista de la escena | `MonstersPrototypeSceneCreator.ValidatePrototypeSceneFromCommandLine` |
| Setup Desktop Hunter Camera | Cámara desktop que sigue al hunter | flag `-setupDesktopHunterCamera` |
| Inspect Source Models | Reporta modelos/animaciones realmente importados | `MonsterModelReporter.InspectFromCommandLine` (flag `-phase3Inspect`) |
| Setup Enemy System / Validate Enemy System | Crea/valida el sistema de enemigos | `SetupEnemySystemFromCommandLine` / `ValidateEnemySystemFromCommandLine` (flag `-phase2Setup`) |
| Setup Survival Systems / Validate Survival Systems | Carga, gigante y HUD de sobrevivencia | `MonsterSurvivalSetup.RunBatchSetupFromCommandLine` |
| Run Phase 2 Deterministic Verification | Verificación determinista (fase 2) | flag `-phase2Verify` / `RunFromCommandLine` |
| Run Phase 2 Play Mode Smoke Test | Smoke test en Play Mode (fase 2) | flag `-phase2PlaySmoke` |
| Run Phase 3 Deterministic Verification | Verificación determinista de sobrevivencia | flag `-survivalVerify` / `RunFromCommandLine` |
| Run Phase 3 Play Mode Smoke Test | Smoke test en Play Mode (sobrevivencia) | flag `-survivalPlaySmoke` / `RunFromCommandLine` |

`MonsterBatchGate` orquesta corridas batch. Todos los tools son re-ejecutables y no pisan cambios manuales del Inspector.

## 9. Estado vs. diseño — gaps conocidos (2026-09-23)

- **Curvas del camino**: `ForestRoad`/`ThreeLaneModel` avanzan recto hacia −Z; el diseño pide un camino con curvas cuyos carriles sigan la traza. Pendiente.
- **Gigante por velocidad**: `GiantZombieSpawner` lanza al gigante desde el inicio; falta el trigger "si la velocidad cae o se detiene" y el **tinte rojo oscuro de derrota** (ya existe `GiantZombieController.GiantCaughtCart`).
- **Bate agarrable y puños XR**: falta el interactor XR del palo (2–3 golpes) y de puños (5–7 golpes). Base lista: `SwordDamage` (`damage`, `sweepRadius`, `ConfigureVelocityReference`).
- **Victoria (reino con torres)**: sin implementar.
- **Carga de monstruos sobre la carreta VR**: `CarriageMotor` aún no consume `ICartSpeedPenaltyReceiver`.
- **Modelos de caballos/carreta/reino**: hoy hay placeholders (`Mat_HorsePlaceholder`, primitivas, `CarriagePrototype.prefab`). Al importar modelos reales usar el skill `hz-unity-fbx-import`.
- **Iluminación nocturna definitiva** (lámparas traseras/delanteras + luna): el prototipo usa velas y una luz "Moonlight"; la escena VR tiene `Directional Light` + `Global Volume` (WIP del equipo).

## 10. Skills

- **De este proyecto (úsalos siempre que apliquen)**:
  - `/jdh-dev-loop` — ciclo de desarrollo: Unity CLI + Editor en vivo, recompilar, tests, verificaciones y XR Simulator (sin builds).
  - `/jdh-reins-gestures` — riendas, gestos, carriles, obstáculos y carreta.
  - `/jdh-monsters` — crear/extender monstruos, spawners, daño y el gigante.
  - `/jdh-xr-hand-interactions` — interacciones 100% con hand tracking y configuración Quest.
- **Ya instalados y relevantes**: `unity-cli`, `unity-pipeline`, `hz-xr-simulator-setup`, `hz-unity-meta-core-sdk`, `hz-unity-code-review`, `hz-unity-fbx-import`, `hz-unity-placement`, `hz-vr-debug`, `metavr-cli`, `hz-unity-project-analyzer`.
