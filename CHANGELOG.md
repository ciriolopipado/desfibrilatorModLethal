# Historial

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
