# Pruebas dentro de Lethal Company v81 — pendientes

Registrar versión del juego, versiones del mod y dependencias, anfitrión/cliente, resultado y ambos `BepInEx/LogOutput.log`. Ejecutar primero con BepInEx + LethalLib y sus dependencias; después con una copia del perfil Lloreria LETHAL 2.

| Caso | Resultado esperado |
| --- | --- |
| Carga del plugin, compra y entrega | Aparece Desfibrilator a 100 créditos, resta 100 y llega el objeto correcto. |
| Aspecto, suelo y equipamiento | Modelo visible sin materiales rosas, orientación y escala adecuadas; se puede coger, soltar y cambiar de ranura. |
| Muere un cliente, lo revive el anfitrión | Tras 5 s vuelve con 30 HP, cámara propia, movimiento, colisiones y voz normales. Los demás lo ven vivo. |
| Muere el anfitrión, lo revive un cliente | Mismo resultado; no depende de que el usuario del objeto sea el anfitrión. |
| Más de cuatro jugadores | Selección y reanimación correctas, sin cambiar el estado de otro jugador. |
| Inventario antes y después | Los objetos permanecen donde cayeron, con la misma cantidad y estado; ninguna duplicación. |
| Soltar E a los 4,9 s | No revive, no consume espera y el siguiente intento necesita 5 s completos. |
| Dejar de apuntar, alejarse, guardar/soltar objeto o morir | Cancelación inmediata y sin reanimación tardía. |
| Dos unidades, dos usuarios, dos cadáveres | Solo puede completarse una carga; todas las unidades quedan bloqueadas 90 s. |
| Cambiar de portador o soltar durante espera | La espera sigue transcurriendo y muestra la misma cuenta atrás. |
| Se incorpora un cliente durante la espera | Recibe el estado de indisponibilidad y no puede eludirlo. |
| Segundo uso tras 90 s | Permite una nueva carga de 5 s; admite volver a revivir a un jugador que murió de nuevo. |
| Cadáver ausente, destruido, decapitado o retenido | No permite desfibrilar; no recupera jugadores sin cadáver válido. |
| Cadáver al otro lado de una pared | No carga ni revive a través de la pared. |
| Cadáver trasladado entre interior y exterior | Revive junto al cuerpo y conserva los efectos de entorno correctos. |
| Cadáver en nave/ascensor | Posición, parentado y movimiento correctos al mover la nave/ascensor; requiere atención especial. |
| Final de ronda/desconexión del objetivo durante carga | Cancela sin revivir jugadores desconectados ni de la siguiente ronda. |
| Otro objeto o manos vacías | E recoge el cadáver como en el juego normal. |
| Tecla personalizada | Carga y cancelación con la nueva tecla, sin acciones de juego accidentales. |
| Sonidos y presentación | Otros jugadores oyen carga/descarga y ven el objeto en uso; cancelar corta la carga. |
| Poltergeist | Revivir elimina la capacidad/representación de fantasma y restaura controles, cámara y voz. |
| HotbarPlus, MoreCompany y HUD/cámara | Ranura activa, cambios de objeto, recuento de vivos y vistas correctos. |
| ReviveDesyncPatch y final de jornada | Sin bloqueo de fin de ronda ni cambios incorrectos a playersRevived. |
| Nueva muerte del jugador reanimado | Crea un único cadáver nuevo y mantiene el flujo de espectador y de fin de ronda. |

## Validado en este entorno

- Compilación con snapshot público de referencias (no verificación exacta de v81).
- Pruebas automatizadas de la lógica independiente de Unity, descritas en README.
- Preparación repetible de referencias con verificación SHA-256 y empaquetado sin DLL del juego.

Ninguna fila de la tabla anterior se considera superada hasta probarla dentro del juego. La animación propia de manos sobre el pecho está pendiente de implementación; actualmente hay pose básica y movimiento procedural de palas.
