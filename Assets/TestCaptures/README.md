# Capturas de la prueba jugable

- `01-inicio.png`: carreta y camino al iniciar Play Mode.
- `02-game-over.png`: derrota activada mediante `DeathScreenEffect.TriggerDefeat()`.
- `03-victoria.png`: victoria activada mediante `LevelVictoryController.Complete()`.

Se generan con **Tools > Game > Capture Playable Smoke**. En modo batch, el simulador XR sitúa a veces el ojo virtual debajo de la carreta; por eso la captura usa una cámara de prueba independiente en la carreta. No mueve la cámara del jugador ni el XR Origin. La prueba comprueba la presentación y las transiciones de estado, no una partida completa con gestos reales en Quest.

Después de la primera captura, la prueba crea un monstruo, le aplica el golpe final y comprueba que su cuerpo activa colisión y `Rigidbody` dinámico antes de probar derrota y victoria.
