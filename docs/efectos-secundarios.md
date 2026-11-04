# Últimas palabras y descargas — 0.2.0 experimental

Solo se marcan como afectados los jugadores reanimados con Ciriolator. El anfitrión mantiene dos temporizadores independientes: voz cada 60–120 segundos y descarga cada 45–90 segundos. Volver a morir suspende los efectos; otra reanimación renueva los temporizadores sin acumularlos. Se borra el estado al volver a órbita o desconectar.

## Voz y consentimiento

La captura está **desactivada por defecto en cada equipo**. El jugador pulsa **F8** para activarla antes de morir; aparece una notificación. También puede cambiar `LastWords.RecordOwnVoice` en `BepInEx/config/ciriolopipado.desfibrilator.cfg`. El consentimiento se recuerda en ese archivo; las grabaciones no. La tecla es configurable mediante `Controls.ToggleLastWordsRecording`.

La integración usa `DissonanceComms.SubscribeToRecordedAudio` y `IMicrophoneSubscriber`, sin abrir un segundo micrófono. Captura únicamente la entrada local cuando el jugador está vivo, en una expedición, sin silenciarse y transmitiendo por un canal de voz del juego. No captura otros jugadores ni recupera audio anterior a activar la función. Suspende la captura mientras reproduce el eco propio para reducir realimentación.

Se conserva la última intervención delimitada por silencio y un fragmento de **hasta 5 segundos**, con un mínimo de 0,25 segundos de habla para descartar ruidos breves. No hay reconocimiento de palabras, transcripción ni clonación de voz: una frase larga puede quedar recortada a sus cinco segundos finales. Se convierte a PCM mono de 8 kHz/16 bits (máximo 80.000 bytes por fragmento). La voz queda deliberadamente estrecha, con un filtro suave y una pequeña variación de tono.

Al detectar una muerte, el anfitrión concede al propietario un permiso de subida único, de diez segundos y ligado a la expedición y a esa muerte. Valida emisor, identificador, plazo, tamaño y longitud antes de reservar el búfer. El audio viaja en fragmentos de hasta 384 muestras (768 bytes), con un máximo de cuatro mensajes de audio por fotograma y reensamblado ordenado; no se envía como un único mensaje grande. Rechaza duplicados y muestras superiores al límite. No se confía en un identificador de jugador enviado por el cliente para escoger al propietario.

El fragmento se comparte por el transporte de la partida con el anfitrión; tras reanimar, se distribuye a los compañeros conectados para reproducirlo en proximidad. Un jugador que se incorpora después recibe los estados y fragmentos afectados vigentes. Son datos de voz compartidos con esos participantes, no una grabación exclusivamente privada en el equipo del hablante. No se crean archivos de audio ni se envían a servicios externos. Las políticas de otros mods de voz no se modifican.

F8 desactiva el consentimiento, borra el búfer propio y solicita la retirada de la copia del anfitrión y de las cachés de los compañeros. La propagación a otros equipos depende de la conexión de red. Al llegar a órbita se destruyen los clips y las fuentes de audio y se vacían los búferes. No se reproducen voces desde cadáveres ni se mezclan grabaciones de expediciones anteriores.

## Proximidad y enemigos

La reproducción usa una fuente 3D independiente, unida al jugador revivido, con unos 12 metros de alcance. No llama al sistema de walkie-talkies ni transmite la frase como si el jugador hablara por radio. Respeta el silencio local del jugador cuando Dissonance lo proporciona.

Mientras dura la reproducción, el anfitrión llama a `RoundManager.PlayAudibleNoise` aproximadamente una vez por segundo, desde la posición del jugador. Puede alertar a enemigos con respuesta al sonido, según sus propias reglas y obstáculos. No convierte a todos los enemigos en perseguidores ni sustituye su inteligencia artificial. Escuchar o reaccionar a estos ruidos requiere validación en juego, especialmente con enemigos añadidos por mods.

## Cámara

La descarga suena cerca del afectado. Solo el propietario modifica su mirada: giro horizontal aleatorio de 4–10 grados y vertical de 2–6 grados, con signos independientes. El nuevo ángulo queda aplicado hasta que el jugador mueve la vista. Se limita la inclinación vertical para evitar invertir la cámara. No causa daño, no bloquea movimiento y no recentra la vista.

`SideEffects.CameraStrength` admite de 0 a 1; 0 desactiva el movimiento en ese cliente por accesibilidad. No se mueve la cámara durante menús de terminal o animaciones que bloquean la mirada.

## Diagnóstico y validación

- Sin voz: activar F8 **antes** de hablar/morir, comprobar que la voz se transmite realmente y buscar `local last-words capture enabled` en el registro. Si no hay frase válida, las descargas funcionan pero no se emite una voz vacía ni su ruido.
- Sin descargas: comprobar versión en todos los participantes, estado afectado, temporizadores e intensidad mayor que cero. Esperar al menos 45 segundos después de reanimar.
- Se compila contra las API públicas de `LethalCompany.GameLibs.Steam 81.0.5-ngd.0`, que incluyen Dissonance. Para las referencias de módulos Unity se usan API 2021.3.33 compartidas. No se distribuyen esas DLL de referencia.
- Hay 35 pruebas de lógica para reanimación, frases acotadas, intervalos, limpieza y permisos de subida. La captura real, la transferencia fragmentada, la cámara, la reacción de enemigos y la convivencia con Mirage/otros mods **no se han ejecutado dentro del juego en este entorno**. Consultar la lista de `pruebas.md`.

Todos deben instalar 0.2.0. El protocolo de reanimación cambió para impedir mezclar versiones anteriores. Se mantienen el precio temporal de 0 créditos, cinco segundos de carga, 30 puntos de vida y espera global de 90 segundos.
