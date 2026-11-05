# Historial

## 0.3.0 — descargas intensas y chispas sobre el cuerpo

- Ecos y descargas pasan a intervalos independientes de 15–30 segundos, tanto al activar como al repetirse.
- Giro horizontal aleatorio de 90–180° con intensidad predeterminada, sin recentrar; conserva una inclinación vertical limitada.
- Un 50 % por descarga solicita conjuntamente caminar 0,75 s en dirección aleatoria y un salto nativo. Solo el propietario aplica el movimiento con física/animaciones normales; se cancela en estados bloqueados, muerte, órbita y desconexión.
- Arcos eléctricos 3D originales durante 0,75 s unidos solo al torso del resucitado, visibles en ese cuerpo para los compañeros. Sin efectos superpuestos en sus pantallas ni movimiento de sus cámaras.
- Protocolo de descargas ampliado con decisión y dirección elegidas por el anfitrión; todos deben actualizar a 0.3.0 y reiniciar.
- 48 pruebas de lógica y compilación con API v81. El usuario confirmó buen funcionamiento de 0.2.2; estas mejoras requieren nueva prueba en juego.

## 0.2.2 — corrección candidata de reanimación

- Restaura controlador de animación, parámetros, sincronización y oyente de audio; mantiene el cuerpo local en sombras en vez de desactivar sus renderizadores y saltarse la inicialización nativa.
- Repara filtros/vinculaciones de voz tras la reanimación sin reiniciar Dissonance ni cambiar el silencio elegido por el usuario.
- El estado vivo recibido no depende de que el cadáver siga presente. Añade identidad de jugador, secuencia, confirmaciones y reintentos acotados; evita aplicar dos veces la misma reanimación.
- Captura ligada a los emisores reales de voz, aislada de excepciones del suscriptor. Los fallos de captura/reproducción no detienen el planificador de descargas.
- Ecos y descargas usan fuentes SFX propias y registros de diagnóstico. No se reutiliza el mezclador de voz en directo.
- 41 pruebas de lógica; requiere nueva prueba en juego de anfitrión y cliente. Todos deben actualizar por el cambio de protocolo.

## 0.2.1 — captura automática

- Captura automáticamente la voz propia transmitida durante la expedición; ya no requiere F8.
- Usa `LastWords.AutomaticVoiceCapture=true` por defecto, también al actualizar desde 0.2.0; ignora las antiguas claves de F8/RecordOwnVoice. Puede desactivarse en la configuración local.
- Conserva la última intervención anterior aunque no se hable justo antes de morir, y la mantiene ante reinicios del procesamiento del micrófono hasta que termine la expedición.
- Mantiene los avisos visibles, el límite de cinco segundos, el silencio/PTT y la limpieza al llegar a órbita.

## 0.2.0 — efectos secundarios experimentales

- Últimas palabras: hasta 5 s de voz propia transmitida antes de morir, con consentimiento local (F8), reproducción cercana cada 60–120 s y ruido para enemigos sensibles al sonido.
- Descargas aleatorias cada 45–90 s que desvían la cámara sin recentrarla; intensidad configurable por accesibilidad.
- Audio en memoria, sin usar grabaciones de Mirage ni servicios externos; revocación y limpieza en órbita/desconexión.
- Validación de emisor, expedición, muerte, plazo y tamaño de cada subida de audio; caché acotada y sin duplicados.
- Referencias públicas de juego v81 y Dissonance; 35 pruebas de lógica independientes de Unity.
- Mantiene Ciriolator a 0 créditos para pruebas. Protocolo incompatible con versiones anteriores: actualizar todos los equipos.

## 0.1.2 — pruebas

- Precio temporal de 0 créditos para validar dentro del juego.
- Nombre visible en la tienda, el objeto y los mensajes: Ciriolator.
- Conserva la identidad interna del mod y del objeto para actualizar la instalación existente.

## 0.1.1 — experimental

- Carátula original de un desfibrilador, con icono de paquete de 256 × 256 píxeles.
- Compilación sin la opción de código inseguro que no se utiliza.
- Revisión estática documentada en SECURITY.md; no equivale a un análisis antivirus ni confirma un falso positivo.
- La descarga 0.1.0 se conserva sin cambios. Ambas versiones siguen sin firma Authenticode.

## 0.1.0 — experimental

- Proyecto C# de BepInEx, con Lethal Company v81 como objetivo y LethalLib 1.2.0.
- Desfibrilator comprable por 100 créditos: carga de 5 segundos, 30 puntos de vida y espera global de 90 segundos.
- Validación del uso en el anfitrión, cancelaciones y sincronización por Unity Netcode.
- Modelo sencillo y sonidos originales generados por código.
- Paquete ZIP para importación local en Thunderstore Mod Manager/r2modman.
- Scripts de compilación y preparación de referencias; 13 pruebas de lógica de servidor.

Compilación validada con referencias públicas de la época de v80. Las pruebas en v81, la compatibilidad con el perfil completo y la animación propia de manos siguen pendientes. Consulta README.md y docs/pruebas.md.
