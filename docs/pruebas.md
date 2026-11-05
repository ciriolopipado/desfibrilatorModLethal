# Pruebas dentro de Lethal Company v81 — pendientes

Registrar versión del juego, versiones del mod y dependencias, anfitrión/cliente, resultado y ambos `BepInEx/LogOutput.log`. Ejecutar primero con BepInEx + LethalLib y sus dependencias; después con una copia del perfil Lloreria LETHAL 2.

| Caso | Resultado esperado |
| --- | --- |
| Carga del plugin, compra y entrega | Aparece Ciriolator a 0 créditos, permite comprarlo sin saldo y llega el objeto correcto. El precio es temporal para pruebas. |
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
| Captura automática | Al entrar aparece aviso y se captura voz propia transmitida sin pulsar F8. Probar con configuración nueva y con un archivo de 0.2.0 que tuviera RecordOwnVoice=false: ambos deben activarse automáticamente. |
| Desactivación local | AutomaticVoiceCapture=false desactiva la captura; si se recarga en partida se eliminan las copias propias y se corta la reproducción tras propagarse la orden. F8 ya no modifica la captura. |
| Frase anterior a la muerte | Hablar, dejar de hablar y morir después: se utiliza la última intervención válida de la expedición, también si se reinició el procesamiento del micrófono. Sin habla no debe inventar audio. |
| Silencio y pulsar para hablar | Hablar con micrófono silenciado o sin transmitir por PTT no aparece en las últimas palabras. No se captura a otros jugadores. |
| Última intervención | Hablar varias frases, morir y revivir: se escucha solo el fragmento de la última, máximo 5 s; no incluye silencio final ni intervenciones viejas. |
| Voz cada 15–30 s | Medir varias reproducciones después de revivir; no se reproducen voces vacías si no hubo frase válida. |
| Fuente de proximidad | Solo se oye cerca del revivido, siguiendo su posición, no por los walkie-talkies. Respetar jugadores silenciados localmente. |
| Atracción de enemigos | Un enemigo sensible al ruido detecta una reproducción cercana según sus reglas. Comprobar también con puertas de nave cerradas y enemigos del perfil. |
| Descarga cada 15–30 s | El afectado oye el chispazo y su cámara gira 90–180° con CameraStrength=1 y queda desviada, sin recentrado ni daño. No mueve la cámara de otros jugadores. |
| Chispas sobre el cuerpo | Observadores ven arcos solo sobre el torso del resucitado, siguiendo su movimiento. No hay chispas en sus propias pantallas ni a través de paredes. |
| Convulsión del 50 % | En algunas descargas, el dueño camina 0,75 s y solicita un salto; ambos observan animación y posición normal, sin duplicar movimiento. |
| Fin de movimiento y restricciones | Tras 0,75 s, muerte, menú, terminal, escalera o fin de ronda se recupera el control normal; nunca se ejecuta una convulsión pendiente al cerrar un menú. |
| Accesibilidad y menús | CameraStrength=0 no mueve la vista; el terminal/animaciones que bloquean mirada no quedan alterados. |
| Segunda muerte y reanimación | Suspende los efectos al morir, toma nuevas últimas palabras y renueva los intervalos al reanimar sin acumular efectos. |
| Llegada a órbita | Corta reproducciones, borra clips y estado; no quedan voces o descargas en la siguiente expedición sin otra reanimación. |
| Incorporación/desconexión | Un cliente que llega recibe los afectados y sus voces vigentes; al salir un jugador se retiran sus datos. |
| Audio y red | Probar frases de 5 s con latencia/pérdida de paquetes y varios jugadores; una transferencia de voz no debe romper la reanimación ni bloquearla. |
| Convivencia con Mirage | No importa sus archivos ni cambia su grabación; las dos suscripciones de Dissonance siguen funcionando. Revisar logs de ambos mods. |

## Validado en este entorno

- Compilación con referencias públicas de juego v81 (81.0.5-ngd.0), Dissonance y módulos Unity de referencia 2021.3.33. No es ejecución del juego.
- 48 pruebas automatizadas de lógica independiente de Unity, descritas en README.
- Preparación repetible de referencias con verificación SHA-256 y empaquetado sin DLL del juego.

Ninguna fila de la tabla anterior se considera superada hasta probarla dentro del juego. La animación propia de manos sobre el pecho está pendiente de implementación; actualmente hay pose básica y movimiento procedural de palas.

La prueba actual de las mejoras está en [pruebas-0.3.0.md](pruebas-0.3.0.md). El usuario informó de buen funcionamiento de 0.2.2; la tabla no sustituye validar las novedades de 0.3.0.

La secuencia histórica de regresión específica para los fallos reportados está en [regresion-0.2.2.md](regresion-0.2.2.md). Se distingue recepción del estado vivo, restauración de animador/oyente y los efectos posteriores.
