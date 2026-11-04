# Prueba de regresión de reanimación — 0.2.2

Esta versión corrige rutas de código compatibles con los fallos comunicados: piernas/pasos congelados, voz en directo ausente y efectos que no se producen. **No se ha ejecutado Lethal Company en el entorno de compilación.** Las pruebas de lógica no certifican animaciones, sonido ni la interacción real con el perfil.

## Preparación

1. Actualizar **todos los participantes** a 0.2.2, eliminar instalaciones manuales duplicadas y reiniciar el juego. El nombre del paquete sigue siendo Desfibrilator y el objeto se llama Ciriolator.
2. Usar una copia del perfil y de la partida. Ciriolator continúa a **0 créditos**. Hablar por la voz normal de la partida, respetando silencio y pulsar para hablar: no hace falta F8.
3. Mantenerse a unos metros para comprobar proximidad. Antes de morir, confirmar que ambos oyen pasos y voz en directo.

## Anfitrión primero

1. El anfitrión dice una frase de varios segundos y muere dejando un cadáver recuperable.
2. El cliente lo reanima. Dejar terminar unos segundos de inicialización/animación del juego.
3. El anfitrión camina y corre: comprobar piernas y pasos tanto desde su vista como desde la del cliente. Comprobar que ambos pueden hablar y oírse en directo sin depender de un eco.
4. Mantener la expedición activa **al menos 120 segundos**. La primera descarga está programada a los 45–90 s; el primer eco a los 60–120 s si se capturó una frase. No despegar antes, porque llegar a órbita elimina los efectos.
5. Confirmar chispazo, desvío de cámara que permanece y reproducción de la voz cerca del anfitrión. CameraStrength debe ser mayor que cero y no debe estar usando el terminal ni una interacción que bloquee la mirada.

## Cliente después

1. El cliente dice otra frase y muere. Comprobar que sigue oyendo al anfitrión vivo al espectarlo o estando cerca de él; la distancia y el modo fantasma de Poltergeist pueden afectar la escucha.
2. El anfitrión reanima al cliente cuando haya terminado la espera global de 90 segundos.
3. Repetir las comprobaciones de piernas, pasos, comunicación en directo y efectos durante 120 segundos.
4. Al volver a órbita, confirmar que se cortan y borran los ecos y descargas. No deben reaparecer en la siguiente expedición sin otra reanimación.

## Registros para diagnosticar

Conservar **los dos** `BepInEx/LogOutput.log` de esa partida y anotar quién murió, qué observó cada uno y cuánto se esperó. No incluyen las palabras grabadas; los mensajes del mod indican identidades de cliente, estado y duraciones.

- `Ciriolator revive applied`: se restauraron las banderas básicas en ese equipo.
- `Ciriolator revive receipt`: el anfitrión recibió confirmación de un observador. Si falta, comprobar versiones y los errores del otro equipo.
- `Ciriolator post-revive check`: controlador de animación y ubicación del oyente después de la inicialización.
- `Ciriolator last words frozen`: duración conservada en el equipo del fallecido. Cero muestras indica que no se obtuvo habla transmitida válida.
- `Ciriolator last words received`: el anfitrión terminó de recibir ese fragmento.
- `Ciriolator effects activated`: segundos hasta el primer eco y descarga, y muestras disponibles en ese momento. Una subida puede finalizar después de este mensaje.
- `Ciriolator echo playing` / `shock received` / `camera impulse applied`: recepción y reproducción/aplicación del efecto.
- `failed`, `rejected stale revive`, `no confirmation`: adjuntar el error completo, no solo la última línea.

Si el fallo solo aparece con el perfil completo, comparar con un perfil mínimo que contenga BepInEx, LethalLib y sus dependencias, y el mod. No se desactiva ni modifica automáticamente ningún otro mod. La revisión de código contempla las rutas de cámara de Poltergeist, pero su compatibilidad todavía necesita esta prueba real.
