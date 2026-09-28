---
name: jdh-xr-hand-interactions
description: Implementa interacciones 100% con hand tracking para Meta Quest 2 en Japanese Demon Hunter — agarres con Interaction SDK, arrojar, golpear con bate o puños vía SwordDamage, y gestión de OVRProjectConfig/AndroidManifest. Úsalo cuando se agreguen manos, agarres, armas, golpes o cualquier interacción XR.
---

# jdh-xr-hand-interactions

Interacciones XR del proyecto. Contexto en [`AGENTS.md`](../../../AGENTS.md) §2 (reglas), §4 (config XR). Dos invariantes: **solo hand tracking** (sin controles Meta) y **jamás mover cámara/XR Origin artificialmente**.

## Stack (verificado)

- Hand tracking por **OVRPlugin + `MetaXRFeature`** (OpenXR) y **Meta Interaction SDK** (`com.meta.xr.sdk.interaction`). Los features "Hand Tracking" de Unity OpenXR (`com.unity.xr.hands`) están deshabilitados a propósito — no los actives sin evidencia de que algo los necesita.
- `OVRProjectConfig.handTrackingSupport = HandsOnly (2)`, `handTrackingFrequency = LOW` (probar `HIGH`/60 Hz si los gestos rápidos — latigazo — se sienten lentos: es un toggle de balance).
- No instalar `com.unity.xr.oculus` (conflicto con OpenXR) ni `com.meta.xr.simulator` (deprecado).

## Referencia de agarre: `Reins/ReinHandle.cs`

Patrón base para agarrables: `HandGrabInteractable` + recorrer `interactable.SelectingInteractors`, leer `interactor.Hand` (`Oculus.Interaction.Input.IHand`: `Handedness`, `IsConnected`, `IsTrackedDataValid`). Solo la mano esperada y con tracking válido conduce. Para un objeto agarrable nuevo (palo/bate, soga, rocas): mismo patrón + `Rigidbody` cuando deba ser arrojado, y decide en el release si vuelve a un reposo (como `ReinHandle`) o queda en física libre.

## Combate XR: bate y puños sobre `SwordDamage`

- API de daño ya lista (`Assets/Scripts/Monsters/SwordDamage.cs`): barrido de cápsula del recorrido de la punta, `damage`, `sweepRadius`, `targetMask`, y **ventanas de ataque**:
  - Por velocidad: activa `allowVelocityActivatedWindows` con `minimumSwingSpeed` y **siempre** `ConfigureVelocityReference(transformDeLaCarreta)` — si no, el simple movimiento de la carreta abre ventanas falsas.
  - Explícitas: `BeginAttackWindow()` / `EndAttackWindow()` desde el interactor (recomendado para puñetazos concretos).
- Números de diseño (calibrar por Inspector, verificar contra `MonsterDamageable.MaximumHealth`): **palo/bate = 2–3 golpes**, **puños = 5–7 golpes**. Ej.: vida 30 → bate `damage` 10–15, puños `damage` 4–6.
- El bate se **agarra con mano** (HandGrabInteractable) y puede **perderse/lanzarse**: al quedar sin bate, los puños siguen funcionando (otro `SwordDamage` con parámetros de puño, sin prop).
- El driver desktop `PrototypeSwordController` es solo referencia de integración de ventanas; el código real debe vivir en el interactor de manos.

## Flujo obligatorio al cambiar configuración Meta (OVRProjectConfig / OVRManager)

1. Cambiar el valor (Inspector o `SerializedObject` sobre `Assets/Oculus/OculusProjectConfig.asset`).
2. **Regenerar el manifest**: `OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(true)` (nunca editar `Assets/Plugins/Android/AndroidManifest.xml` a mano para features gestionadas por OVRProjectConfig).
3. **Verificar** que el manifest contiene `oculus.software.handtracking` (required) y el permiso `com.oculus.permission.HAND_TRACKING`.

### Quirks de Unity MCP para llamar al SDK (reflexión)

- `mcp__unity__eval` espera un **cuerpo de método** (sin `using` al inicio; tipos fully-qualified). El `timeout` va **en milisegundos**.
- **Nunca** `using System.Reflection;` (crashea el harness); cualifica `System.Reflection.*`.
- **Sin** overloads de `GetMethod`/`GetProperty` con `BindingFlags`; usa `GetMethod("Nombre")` y `GetRuntimeFields()` para campos privados.
- Siempre `silentMode: true` en métodos con diálogos (`EditorUtility.DisplayDialog` bloquea). Captura `System.Reflection.TargetInvocationException` e imprime `InnerException`.

## Pruebas y límites

- Meta XR Simulator: valida flujos con poses de mano **discretas**; **no** valida umbrales de agarre, calidad de latigazos ni física de agarre fino. Decláralo siempre en el informe y valida en Quest real (el humano compila).
- Rendimiento Quest 2 (no lo regresiones): URP `Mobile_RPAsset` con MSAA 4x y render scale 0.8; pocas luces puntuales (las lámparas de la carreta son las fuentes principales de noche); evitar transparencias overdraw.
- Movimiento del jugador: caminar sobre la carreta es locomoción natural del usuario; prohíbido teletransportar o empujar al jugador/cámara por código.
