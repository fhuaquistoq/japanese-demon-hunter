# Japanese Demon Hunter — AGENTS.md

> Guía de trabajo para agentes de código. Léela antes de tocar nada en este proyecto.
> Última actualización: 2026-09-26 (código actual, escena regenerada y 120/120 EditMode verificados; el headset no se verificó visualmente en Quest).

## 1. Diseño vigente y estado comprobable

El jugador viaja en una carreta tirada por dos caballos, con interacción de manos. Separá las decisiones del producto de las capacidades heredadas del código:

- Las riendas se agarran con ambas manos (izquierda/derecha asignadas); un agarre inválido no conduce. La gracia de selección de 0,12 s congela solo la pose visual, nunca habilita gestos. En la escena, el SDK mueve el objeto hijo agarrable y `ReinHandle` escribe el pin padre: no comparten el mismo transform.
- Latigazo bilateral hacia abajo acelera; tirar hacia los lados cambia suavemente entre **3 carriles curvos** mediante `RoadPathModel`. Frenar no forma parte del diseño vigente y está desactivado por defecto. Las rocas del camino tampoco forman parte del juego actual (`buildRoadRocks` queda apagado); conservá el código heredado, no lo reactives.
- Zombis llegan desde atrás, pueden engancharse y penalizan la velocidad. Los murciélagos llegan por un carril frontal seleccionado y pueden amenazar la cara a unos **0,9 m**; una mano rastreada que se agita cerca los despeja. Si no se despejan, el timeout aplica una penalización temporal segura y retira la amenaza.
- El arma es un **cuchillo agarrable**, no un bate de madera; también hay daño con puños rastreados. No fijes conteos de golpes sin validar el balance vigente.
- **Velocidad y ritmo de los caballos**: la velocidad máxima es `HorseMaximumSpeed` (**8 m/s**) y la ganancia por latigazo se deriva de ella (`(máx − inicial) / 3`), así que **con 3 latigazos se llega al máximo** y el cuarto no suma; el arranque está en 3,5 m/s. Los clips del caballo están declarados como **looping en el importer** (Idle/Walk/Gallop), y `HorseAnimationDriver` mantiene una tasa de reproducción mínima (0,35) para que el idle no se congele con la carreta parada.
- La **carreta** usa el modelo real `Wagon_Model` (Wild West Cart) girado 90° (su largo viene en el eje X del OBJ y los ejes de rueda en Z), ajustado al ancho de **ambos caballos + 0,30 m** y luego escalado por `WagonSizeMultiplier` (hoy **×2**, o sea 4,26 ancho × 6,05 largo × 1,66 alto), con las ruedas sobre el camino; los bloques del `CarriagePrototype` solo se ocultan (sus transforms y colliders siguen leyéndose). La **soga** es beis sin emisión, con damping, y se ancla al `ReinCollarAnchor` del cuello del caballo. Los **caballos** reproducen los clips importados del FBX mediante `HorseAnimationDriver` (Idle/Walk/Gallop según la velocidad real); el `HorseLocomotionDriver` procedural sigue como respaldo.
- Los **anclajes traseros** de monstruos y el **cuchillo** se colocan derivándolos del `Wagon_Model` (borde trasero y mitad de altura), no con offsets fijos: el pescante creció mucho y las posiciones fijas dejaron a los zombies dentro de la caja y al cuchillo enterrado bajo el piso.
- El **gesto de látigo/rienda** usa un punto neutro adaptativo en `ReinHandle` (`settleSpeed`, `recenterSpeed`): la línea base se captura al agarrar, así que sin ese recentrado levantarse después de agarrar sentado dejaba la rienda "levantada" para siempre y ya no se rearmaba.
- La carga de monstruos ya se integra entre `CartMonsterLoad` y `CarriageMotor`. El balance actual configura zombis con carga 15 y multiplicador mínimo 0,3; los 2 tests de balance de J5 quedaron verificados dentro de la corrida 120/120 del 2026-09-26.
- El disparador del gigante por velocidad baja y el efecto de derrota rojo están implementados; también existe la victoria al llegar al reino fortificado.
- Hay cuatro lámparas de carreta, colocadas en las cuatro esquinas del `Wagon_Model` a partir de sus bounds (no con offsets fijos, que quedaban volando), pero su aspecto y rendimiento son candidatos pendientes de validación en Quest. No afirmes pruebas visuales ni de rendimiento de Quest que no se hayan realizado.
- **Altura del `VehicleRoot` (defecto corregido)**: el camino es una calzada elevada sobre el plano de suelo y `CarriageMotor` alinea la carreta contra la posición en la que arranca su root, así que un `VehicleRoot` en y=0 hacía viajar a toda la carreta ~0,35 m por debajo de la superficie. El generador ahora lo sitúa en `HeightAtDistance(0)` del propio camino. No volver a crear el `VehicleRoot` en y=0: hundiría de nuevo carreta, caballos y rig.

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
- `Assets/Scenes/JapanDemonHunter.unity` — escena jugable principal, creada y validada por `PlayableSceneCreator`; incluye el rig, la carreta, riendas, camino, monstruos, combate, lámparas y final. El creador abre una escena existente sin sobrescribirla por defecto. No ejecutar rutas de overwrite/rebuild sobre ella: preservar cambios manuales y no reconstruirla destructivamente. La evidencia reciente de wiring de escena proviene del Editor, pero no equivale a verificación visual/performance en Quest.
- `Assets/Scenes/SampleScene.unity` — escena heredada del equipo; no es la escena principal actual. Preservarla y no modificarla a ciegas.
- `Assets/Scenes/MonstersPrototype.unity` — prototipo desktop generado por `Tools/Monsters/Create Prototype Scene`; no confundirlo con la escena principal ni regenerarlo sobre trabajo manual.
- `Assets/Art/Monsters/{Zombie,Spider,GiantZombie,Ghost,Demon,Bat}` — modelos FBX reales + prefabs (`ZombieDemon`, `SpiderMonster`, `GiantZombie`, `GhostMonster`, `DemonMonster`, `BatDemon`) + Animator controllers (packs de Quaternius y otros). *`Bat` = murciélago demonio, no el arma.*
- `Assets/Art/Wagon/Wild West Cart.obj` — modelo de carreta (OBJ de Maya, mesh único, sin `.mtl`); se instancia como `Wagon_Model` en la escena.
- `Assets/Animations/Horse/Horse.controller` — máquina de estados generada desde los clips del FBX del caballo.
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
| `RopePhysics.cs` | Simulación **Verlet** sin allocs del lazo cerrado de 48 pins: gravedad, damping, suelo (`minimumDeckHeight`), techo de velocidad por paso, iteraciones de restricción con relajación. Puro C# y testeado. |
| `ClosedReinLoop.cs` | Dibuja el lazo con `LineRenderer` (loop, espacio local de `vehicleRoot`, 48 puntos). Pins: cabeza caballo izq. (collar), empuñadura izq., empuñadura der., cabeza caballo der. Suaviza los anchors para que la soga siga al galope sin tirones. |
| `ReinHandle.cs` | Empuñadura por mano: `HandGrabInteractable` (Interaction SDK) + `IHand`. Mide el *pull* respecto a `restLocalPosition`, vuelve al reposo al soltar, expone `ReadGesture(dt)`. |
| `ReinDrivingModel.cs` | Modelos puros de gestos bilaterales, gracia de agarre y transición suave entre tres carriles. Conserva modelos antiguos de freno/rocas: no son parte del diseño activo. |
| `CarriageMotor.cs` | Requiere ambas riendas asignadas para interpretar un gesto; sigue la curva del camino y expone velocidad/carril. Implementa `ICartSpeedPenaltyReceiver` y penalización temporal por impacto. La ruta actual no activa freno ni rocas. |
| `ForestRoad.cs` / `RoadPathModel.cs` | Camino de tres carriles curvos, reciclado por tiles. `buildRoadRocks` está apagado en la escena generada; no habilitar obstáculos pétreos. |

`BilateralReinGestureModel` promedia el movimiento relativo de ambas manos y no emite órdenes si falta una. Los umbrales/cooldowns de gesto están expuestos en Inspector. `ReinHandle` usa el `HandGrabInteractable` hijo como zona SDK; el pin padre sigue la muñeca válida y conserva la última pose visual hasta 0,12 s al perder selección. Esa gracia no cuenta como agarre ni autoriza órdenes. El gesto de freno sigue en código para compatibilidad, pero `enableBrakeGesture` está apagado por defecto y el producto no lo usa.

### 6.2 Monsters — enemigos (ns `JapaneseDemonHunter.Monsters`)

- **`MonsterBase`**: FSM de 12 estados (`Spawn, Patrol, SelectTarget, Chase, Approach, Attack, Retreat, SelectAttachment, ChaseAttachment, Attach, Attached, Dead`) con radios/tiempos por Inspector; eventos `StateChanged`, `BecameInactive`, `Died`; `Kill()` y `Retire()`. Se inicializa con `MonsterSpawnContext`.
- **Movimiento**: `MonsterMovement` (abstracto: patrol/chase/approach + rotación) → `GroundMonsterMovement` (solo camina sobre superficies marcadas con **`MonsterGroundSurface`**, paso máx. 0.35 m — evita subir a la carreta —, esquiva obstáculos ±55°) y `FlyingMonsterMovement` (alturas 1.5–7 m, esquiva y trepa).
- **Spawn**: `MonsterSpawner` con `MonsterSpawnEntry` (prefab, tipo, peso, radios, alturas de vuelo, `attachmentLoad`): uno de cada al iniciar, por intervalo, máximo de activos, colocación sobre `MonsterGroundSurface` con holgura y distancia mínima al jugador, retiro a >90 m. Un **único** spawner cubre todos los tipos.
- **Combate**: `MonsterAttack`, `AttachedMonsterAttack`, `MonsterDamageable` y `SwordDamage` (barrido de cápsula con ventanas de golpe). La escena principal crea un cuchillo agarrable (`GrabbableWeapon`) y `HandStrikeController` para puños rastreados; no es un bate. El prototipo desktop usa `PrototypeSwordController`.
- **Amenazas y carga**: los zombis se spawnean por detrás y pueden reservar puntos traseros con `MonsterAttachment`; la carga se registra en `CartMonsterLoad` y llega al `CarriageMotor` por interfaz. Configuración actual: zombis 15, máximo de referencia 60 y mínimo 0,3. Los murciélagos frontales van por un carril, se presentan a ~0,9 m, se despejan con una mano rastreada o se retiran con penalización temporal al vencer el timeout; no se enganchan a la carreta.
- **Gigante y resultados**: `GiantZombieSpawner` puede activarse tras avanzar y sostener velocidad baja; `DeathScreenEffect` tiñe la vista de rojo al alcanzar el gigante. `LevelVictoryController` completa la partida al llegar al reino fortificado. Los tests de J5 para casos de carga cero/no cero siguen pendientes de ejecución; no confundir código/configuración con verificación.
- **Objetivos**: `IMonsterTarget` (`Hunter`/`Candle` + `TryReceiveHit`) en `MonsterTargetRegistry`, elegidos por `MonsterTargetSelector` (estrategias `Closest`, `ClosestLitCandle`, `RandomLitCandle`, `PrioritizeHunter`). Implementaciones: `PrototypeHunterMonsterTarget` (evento `SimulatedHit`) y `PrototypeCandleMonsterTarget` (apaga `PrototypeCandle`).
- **Animación**: `MonsterAnimationController` — crossfades a estados **por nombre configurable** (`Locomotion`/`Attack`/`Death` por defecto); `HasAttackAnimation`/`HasDeathAnimation` permiten adaptarse a lo que realmente exista en cada controller.

### 6.3 Prototype — prototipo desktop (ns `JapaneseDemonHunter.Prototype*`)

Simulación sin headset para validar gameplay: `SimulatedCartMovement` (movimiento recto temporal), `SimulatedCartSurvivalController` (**implementa `ICartSpeedPenaltyReceiver` + `ICartAccelerationRequester`**; LShift = latigazo de escritorio), `SimulatedHunterMovement` (WASD local; nunca mueve cámara/XR Origin), `SmoothFollowCamera` (mouse-look desktop), `PrototypeCandle` (vela + evento `Extinguished`), `PrototypeSceneReferences` (superficie de integración de la escena), `SurvivalPrototypeHud` (OnGUI). Contratos compartidos en `CartSpeedIntegration.cs`: `ICartSpeedPenaltyReceiver`, `ICartAccelerationRequester`.

### 6.4 Ambientación —Horse y carreta (ns `JapaneseDemonHunter.Gameplay*`)

- **`Assets/Scripts/Gameplay/Editor/HorseImportSetup.cs`** (menú `Tools/Game/Setup Horse Import`): deja el FBX del caballo en `Generic` + `skinWeights Standard`, lee los clips realmente importados y construye `Assets/Animations/Horse/Horse.controller` con Idle/Walk/Gallop. Es idempotente y **no asume nombres de clip**: los clasifica por palabra clave y descarta los takes de reacción/transición (`*_HitReact_*`, `Jump_toIdle`, `*_Jump`, `Death`, `Attack_*`).
- **`HorseAnimationDriver.cs`**: lee la velocidad por `ICartSpeedPenaltyReceiver` y alimenta los parámetros `Speed` y `Gallop` del Animator, escalando la reproducción para que la cadencia siga la velocidad real. Con `useImportedClips` apagado delega en el `HorseLocomotionDriver` procedural.
- **`Wild West Cart.obj`**: un único mesh sin grupos nombrados y sin `.mtl`, con slots `blinn1SG`/`blinn2SG`. Se instancia como `Wagon_Model` y se auto-ajusta por bounds (ancho del deck, apoyo en el suelo, centrado entre el deck y los caballos). Sus ruedas **no se pueden girar**: es un único mesh, no hay nodos separados.

## 7. Flujo de desarrollo diario

1. **Editor vivo**: `unity status` (esperar `state: ready`) y trabajar con `unity command …` / Unity MCP sobre la escena abierta. `ready` por sí solo no confirma que el pipeline responda. **Nunca** editar YAML de `.unity`/`.prefab`/`.asset` con el Editor abierto.
2. **Recompilar y verificar** tras cada cambio de código:
   - `unity command recompile` → `recompile_status` → `console` sin errores.
   - EditMode: usar el runner enfocado cuando corresponda; si el canal síncrono se bloquea, detener reintentos, pedir al humano recarga/reinicio del Editor y retomar con `run_tests` asíncrono (`async_tests: true`) + consulta de estado. No reintentar ciegamente una suite síncrona de 300 s.
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
| Create Prototype Scene | Crea el prototipo si no existe; si existe, solo lo abre. No reconstruir/regenerar escenas existentes de forma destructiva ni sobreescribir cambios manuales. | `MonstersPrototypeSceneCreator.CreatePrototypeSceneFromCommandLine` |
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

Menús de `Tools/Game/*` (`PlayableSceneCreator` y `HorseImportSetup`): `Create Playable Scene`, `Rebuild Playable Scene (backup + overwrite)`, `Validate Playable Scene`, `Setup Horse Import`.

## 9. Pendientes y evidencia reciente (2026-09-26)

- No reintroducir freno ni obstáculos de roca: son decisiones de producto, aunque queden tipos/modelos heredados en el código.
- **Verificado 2026-09-26: 120/120 EditMode en verde** con `run_tests` async, tras regenerar la escena con `Tools/Game/Rebuild Playable Scene`. Incluye los 2 tests de balance de carga de J5 que estaban pendientes. La escena se reconstruyó y pasó su propia validación.
- **Ciclos de animación y velocidad, verificados en Play el 2026-09-26**: el loop del caballo es persistente (`normalizedTime` llegó a 8,24 con `loop=true`, o sea cicló 8 veces) y los latigazos dan 3,5 → 5,0 → 6,5 → 8,0 con el cuarto sin sumar.
- **`AnimationUtility.SetAnimationClipSettings` NO persiste sobre clips de un FBX**: el ajuste se lee bien en memoria y el siguiente reimport lo descarta sin avisar. El loop de un clip importado hay que declararlo en `importer.clipAnimations` (ver `HorseImportSetup.DeclareLoopingTakes`), reasignando todas las tomas con su rango de frames para no perder ninguna.
- **`CarriageMotor.EffectiveSpeed` solo se actualiza en `Update`** (viene de `LoadModel.ReportSpeed`), así que no sirve para medir un latigazo en el mismo frame: usar la propiedad `Speed`, que lee `_speed` directo. Y `LoadModel` se cachea en `Awake`: para probar otros valores hay que crear el componente con el objeto inactivo y activarlo después.
- Ajuste de colocación verificado en Play el 2026-09-26: la carreta pasó de 0,365 m hundida a 0,040 m sobre la superficie del camino; girada 90°, 2,13 m de ancho frente a 1,83 m de los dos caballos, y las 4 lámparas dentro del pescante. Capturas en `Assets/Screenshots/`.
- **Tamaño de la carreta ×2 (a pedido, 2026-09-26)**: quedó en 4,26 × 6,05 × 1,66 m, o sea 2,33 veces el ancho de los dos caballos. Eso **reemplaza** la regla previa de "ancho de ambos caballos + 0,30 m"; el ajuste a los caballos sigue calculándose primero y `WagonSizeMultiplier` lo multiplica. Dos consecuencias conocidas y no corregidas: (a) el origen del rig VR está a 0,695 en local y el piso del pescante ahora queda a 0,83, así que **los pies del jugador quedarían 0,135 m dentro del piso** (antes flotaban 0,28 m por encima; en un HMD real la vista queda igual por encima de las barandas); (b) `RopeAnchors/Anchor_CarriageRear` (0, 1,60, 1,55) quedó **dentro** del volumen del pescante, así que los zombis que se enganchan atrás aparecerían dentro de la caja. Ninguna de las dos se tocó sin pedido explícito.
- **No apagues renderers para renders de diagnóstico sobre esta escena**: el generador deja los bloques del prototipo ocultos a propósito, y un `renderer.enabled = true` masivo de restauración los vuelve a encender (aparecieron de nuevo las cabezas de cubo). Para aislar un objeto, mové la cámara o duplicá el objeto en una escena temporal; si ya pasó, reabrí `JapanDemonHunter.unity` para recuperar el estado guardado.
- Ambientación overhaul verificado: cabezas de cubo ocultas, `Horse.controller` construido sobre los clips reales (`Idle`/`Walk`/`Gallop`) con malla skinned de 50 huesos, soga beis sin emisión con damping, y `Wagon_Model` (Wild West Cart) sustituyendo la carreta de cubos. Capturas en `Assets/Screenshots/`.
- `ModelImporterSkinWeights` en Unity 6 **solo tiene `Standard` y `Custom`**: no existen `Bone`, `BoneAndAutomatic` ni `None`. Usar `Standard` para importar skin weights.
- `AnimatorController.RemoveParameter` toma un **índice de posición**, no un hash; y hay que iterar en descendente porque cada borrado desplaza el resto. Asignar `controller.layers = new AnimatorControllerLayer[0]` deja el controller sin capa y provoca `IndexOutOfRange`; usar `RemoveLayer(0)` en bucle.
- El OBJ del wagon viene de Maya **sin líneas `o`/`g` y sin su `.mtl`**: es un único mesh con sub-mallas `blinn1SG`/`blinn2SG`. Por eso **no hay ruedas separadas y no se pueden girar** (decisión de producto, 2026-09-26). Los materiales se asignan por nombre de slot con fallback a madera.
- El pipeline se desconectó varias veces al recompilar (`Cannot connect to Unity Editor Pipeline server`, puertos 7800/7801) pero `editor_status` volvió a `ready`; reintentar el comando. Los puertos de Meta XR AgentBridge/MCPBridge en conflicto son ruido conocido y no afectan al pipeline.
- **Causa raíz del hundimiento de la carreta (corregida)**: `FirstPersonLocomotor` del interaction rig estaba cableado con `_playerOrigin = VehicleRoot`, así que cuando el rig sin piso "cae" arrastraba el root de la carreta hacia abajo a ~0,13 m/s (se midieron −97 m en pocos minutos). El generador lo **deshabilita** (`DisableArtificialLocomotion`) porque el juego es un viaje sobre la carreta y la regla VR prohíbe mover el XR Origin. Si vuelve a hundirse, comprobar primero ese componente antes de tocar el `VehicleRoot`.
- **Textura del camino**: `mainTextureScale = (1,3)` repite la textura de tierra 3 veces por tile, así que **debe ser periódica**. Con `Mathf.PerlinNoise` crudo dejaba una banda horizontal dura cruzando todo el ancho del camino cada ~6 m; ahora usa `TileablePerlin` (mezcla de las 4 copias desplazadas).
- **El plano `Ground` no tenía material** (gris por defecto de Unity), y eso es lo que hacía que el camino no se distinguiera del borde del bosque. Ahora usa `Mat_ForestFloor`.
- **Bosque**: todas las filas usan los modelos FBX (las primitivas `Trunk`/`Canopy` quedaron solo como respaldo si no hay modelos). Valores: `treeHeight 11`, 4 filas × 6 por fila × 2 lados = 440 instancias contando el bosque trasero.
- **Faroles de carretera**: `post_lantern.fbx` se instancia **por tile** (se recicla con el camino) y sus luces reales sólo se encienden a `lanternLitChunkSpan` chunks de la carreta; el resto quedan apagadas porque encender 16 luces de punto es inviable en Quest.
- **`CarriageLampAmbience` no estaba en la escena generada**: el pulso de la llama sólo lo añadía un menú aparte, así que nunca se aplicaba. Ahora el generador lo cablea al final de la construcción (después de existir los controladores de victoria/sesión) y le fija ahí mismo color, intensidad, rango y pulso. El objeto "Flame" de cada lámpara es lo que se enciende y apaga; el cuerpo del farol siempre queda.
- **La animación del caballo ya no se escala con la velocidad** (`HorseAnimationDriver` deja el `Animator.speed` en su valor natural): acelerarla hacía que el galope fuera por delante de sí mismo ahora que la carreta corre más.
- **Suite EditMode colgada (2026-09-26, tras esta ronda)**: el run se quedó más de 10 minutos y dejó el pipeline sin responder (`editor_status` y `test_status` agotan el tiempo). Es el mismo modo de fallo documentado en J5. Antes de dar por buenos los tests de esta ronda hace falta que un humano recargue/reinicie el Editor y se vuelva a correr con `async_tests: true`. El **último 120/120 verificado es anterior a estos cambios**.
- Respetar propiedad del equipo, no editar YAML de escenas/prefabs/asset con el Editor abierto y no compilar builds.

### Trabajo encolado (pedido, aún sin hacer)

- **Horda trasera siempre visible**: detrás debe verse siempre una horda de zombies, y solo algunos de ellos avanzan más rápido que el resto (esos son los que alcanzan la carreta). El **zombie gigante** también debe verse siempre, caminando por detrás de la horda y un poco más lento. Hoy `MonsterSpawner` no mantiene ningún mínimo de activos, todos los zombis corren a la misma velocidad (`patrolSpeed == chaseSpeed == 6.8` en el prefab) y el gigante se oculta a más de 18 m y sólo aparece una vez.
- **Rondas de murciélagos por delante**: llegan de frente y bloquean la vista hasta que el jugador se los quita con el gesto delante de la cámara. Deben aparecer más seguido, en rondas de 1 o 2 según cuántos carriles queden libres (1 murciélago → 2 carriles para esquivar; 2 → 1 carril). Al esquivar una ronda, la siguiente aparece a los 5 s y tarda 5–8 s más en alcanzar al jugador. Si un murciélago se cuelga del jugador, **no** aparece otra ronda hasta que se lo quite; una vez quitado, siguen los mismos tiempos que si lo hubiera esquivado.
- **Audio 360**: las pisadas del zombie gigante deben oírse casi siempre, mucho más fuertes de cerca y con un ligero temblor de cámara. Los sonidos de zombies y murciélagos tienen que ser claros y avisar de su cercanía. Bajar un poco el volumen de los látigos.

### Hecho en la ronda del 2026-09-26 (segunda tanda)

Poblar el bosque (440 modelos reales, sin primitivas), linternas nuevas en las 4 esquinas de la carreta con pulso de llama y `post_lantern` a cada lado del camino con las luces encendidas sólo cerca, noche más oscura y sin tinte azul, camino color tierra contra suelo de bosque verde, luna más grande con luz más tenue, y galope a velocidad natural sin escalar. Todo ello está en la escena regenerada y pasó la validación del generador; **los tests EditMode de esta tanda quedaron sin ejecutar** por el cuelgue descrito arriba.

## 10. Skills

- **De este proyecto (úsalos siempre que apliquen)**:
  - `/jdh-dev-loop` — ciclo de desarrollo: Unity CLI + Editor en vivo, recompilar, tests, verificaciones y XR Simulator (sin builds).
  - `/jdh-reins-gestures` — riendas, gestos, carriles, obstáculos y carreta.
  - `/jdh-monsters` — crear/extender monstruos, spawners, daño y el gigante.
  - `/jdh-xr-hand-interactions` — interacciones 100% con hand tracking y configuración Quest.
- **Ya instalados y relevantes**: `unity-cli`, `unity-pipeline`, `hz-xr-simulator-setup`, `hz-unity-meta-core-sdk`, `hz-unity-code-review`, `hz-unity-fbx-import`, `hz-unity-placement`, `hz-vr-debug`, `metavr-cli`, `hz-unity-project-analyzer`.
