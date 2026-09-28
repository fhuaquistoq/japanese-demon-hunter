---
name: jdh-dev-loop
description: Ejecuta el ciclo de desarrollo de Japanese Demon Hunter en Unity 6 — Editor en vivo por Unity CLI/MCP, recompilar, tests EditMode, verificaciones Tools/Monsters y pruebas con Meta XR Simulator, siempre sin compilar builds. Úsalo cuando pidan probar, verificar, validar o iterar cambios en este proyecto.
---

# jdh-dev-loop

Ciclo de desarrollo del proyecto. Contexto completo en [`AGENTS.md`](../../../AGENTS.md) (§2 reglas, §7 flujo, §8 tooling). Regla absoluta: **nunca compilar builds** (`unity build`, Build Pipeline) — el humano compila para los Quest reales.

## 1. Preflight (siempre primero)

```bash
unity status --format json        # esperar "state": "ready" y project = este repo
```

- `state: ready` → trabajar contra el Editor en vivo con las herramientas `mcp__unity__*` o `unity command <nombre>`.
- Si **no** conecta y el Editor está abierto, puede estar en **Safe Mode** por errores de compilación: `unity pipeline list` lo indica; arreglar el C# y reiniciar Unity. No edites YAML a ciegas como alternativa.
- Con Editor conectado: **nunca** editar a mano `.unity`, `.prefab` ni `.asset` (usa MCP/`unity command`). Con Editor cerrado, editar archivos fuente `.cs` es aceptable.

## 2. Iteración de código

1. Editar fuentes `.cs` (o crear GameObjects/props con MCP).
2. `mcp__unity__recompile` → `mcp__unity__recompile_status` → `mcp__unity__console` (nivel error): cero errores antes de continuar.
3. Guardar escenas si el cambio es de escena: `mcp__unity__save_scene` / `save_all`.

## 3. Verificación obligatoria tras cambios

Ejecutar el bloque que corresponda; **nunca reportar "verificado" sin haberlo ejecutado**.

| Qué | Cómo (MCP) | Cómo (CLI) |
|---|---|---|
| Tests EditMode (Reins + Prototype) | `mcp__unity__list_tests {mode:"EditMode"}` → `mcp__unity__run_tests {mode:"EditMode"}` | `unity test . --mode EditMode` |
| Escena prototipo | `mcp__unity__menu {path:"Tools/Monsters/Validate Prototype Scene"}` | batch: `-executeMethod JapaneseDemonHunter.PrototypeEditor.MonstersPrototypeSceneCreator.ValidatePrototypeSceneFromCommandLine` |
| Sistema de enemigos | `mcp__unity__menu {path:"Tools/Monsters/Validate Enemy System"}` | flag `-phase2Setup` |
| Fase 2 determinista | `mcp__unity__menu {path:"Tools/Monsters/Run Phase 2 Deterministic Verification"}` | flag `-phase2Verify` |
| Fase 2 Play Mode smoke | `mcp__unity__menu {path:"Tools/Monsters/Run Phase 2 Play Mode Smoke Test"}` | flag `-phase2PlaySmoke` |
| Fase 3 determinista | `mcp__unity__menu {path:"Tools/Monsters/Run Phase 3 Deterministic Verification"}` | flag `-survivalVerify` |
| Fase 3 Play Mode smoke | `mcp__unity__menu {path:"Tools/Monsters/Run Phase 3 Play Mode Smoke Test"}` | flag `-survivalPlaySmoke` |
| Modelos/animaciones importadas | `mcp__unity__menu {path:"Tools/Monsters/Inspect Source Models"}` | flag `-phase3Inspect` |

Los menús `Tools/Monsters/*` son idempotentes y no destructivos. Los flags batch (`-phase2Verify`, `-survivalPlaySmoke`, etc.) están pensados para corridas headless vía `unity run` + `-executeMethod` (los entry points exactos están en AGENTS.md §8).

## 4. Probar con Meta XR Simulator (funcional, NO rendimiento)

1. Es una **app standalone** de escritorio (runtime OpenXR) — descargar desde MQDH → Tools o `developers.meta.com/horizon/downloads/package/meta-xr-simulator-windows/`. **No instalar ningún paquete Unity**: `com.meta.xr.simulator` está deprecado y este proyecto usa OpenXR.
2. Activar en Unity: **Window > Meta > Meta XR Simulator > Activate** (o el icono junto a Play). La consola debe decir `[Meta XR Simulator is activated]`. `Deactivate` para volver al headset real; `Status` para consultar.
3. En la UI del simulador: **Inputs > Device info > Device → Meta Quest 2** (el default es Quest 3). Reiniciar Play Mode tras cambiar de dispositivo (el perfil se captura al crear la instancia OpenXR).
4. Deja el simulador activo entre corridas (solo sale de Play Mode) para iterar más rápido.
5. Si Play Mode no conecta al simulador: revisar que ningún otro runtime OpenXR esté activo (SteamVR/WMR/Oculus PC) — ver gotchas de `hz-xr-simulator-setup`.

Límites que debes declarar al reportar resultados:
- Manos simuladas = **poses discretas por teclado**, no tracking continuo: los umbrales de agarre y gestos finos (latigazo de riendas) **no quedan validados** — eso se valida en el Quest real.
- Sin paridad de rendimiento ni de shaders (GPU Adreno): "funciona en el simulador" ≠ "funciona en dispositivo".

## 5. Informe final (obligatorio)

- Archivos creados/modificados y cómo revertir.
- Comandos de verificación **realmente ejecutados** con su resultado; separar claramente lo que queda pendiente de validación manual (build humano + Quest).
- Puntos de integración tocados (interfaces `ICartSpeedPenaltyReceiver`, `ICartAccelerationRequester`, `IMonsterTarget`) y limitaciones conocidas.
- Sin `git commit` ni `git push`; dejar los cambios sin commitear.
