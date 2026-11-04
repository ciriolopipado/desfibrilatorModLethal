# Seguridad y autenticidad de las descargas

Se ha comunicado un bloqueo de la descarga **0.1.0** como «caballo de Troya», observado en Chrome. No se dispone todavía del identificador completo de detección ni de un resultado verificable del motor que lo emitió. La ausencia de entradas en el historial de Defender no confirma un falso positivo.

**No ejecutar, restaurar de cuarentena ni añadir exclusiones para sortear una detección de malware.** Conservar el nombre exacto del aviso y, si el archivo sigue accesible sin restaurarlo, su SHA-256. El proveedor de la detección debe revisar la muestra para confirmar o descartar un falso positivo.

## Revisión realizada

- Se ha inspeccionado el ZIP publicado 0.1.0 y su código fuente. Contiene dos DLL administradas propias, un PNG y documentación/manifiesto. No contiene ejecutables auxiliares ni scripts de instalación ejecutables.
- Se han comprobado la integridad ZIP, las rutas de sus entradas y sus SHA-256. Las DLL coincidían con los archivos de compilación presentes al comenzar la revisión.
- En la inspección de las DLL no se han encontrado métodos P/Invoke, módulos nativos declarados, recursos incrustados ni llamadas directas a API de descarga/red HTTP, creación de procesos, registro de Windows o acceso a archivos de System.IO. El mod sí usa Unity Netcode para la partida y BepInEx para configuración y registros.
- Usa Harmony y reflexión para modificar métodos del juego. Es parte de la implementación del mod, pero no permite concluir qué regla provocó el bloqueo.
- Las DLL publicadas no tienen firma Authenticode. Tampoco tienen firma strong-name de .NET.
- Esta revisión es estática: **no se ha ejecutado el mod ni un antivirus con base de firmas actualizada en este entorno**. No es un certificado de ausencia de malware y no confirma compatibilidad con v81.

La versión 0.1.1 incorpora una carátula original y desactiva `AllowUnsafeBlocks`, una opción de compilación que el código no necesita. Se ha comprobado que compila sin ella. Esto elimina la marca `UnverifiableCodeAttribute` que emitía el compilador; **no hay evidencia de que esa marca fuese la causa de la detección**. El cambio no elimina el uso de Harmony ni garantiza aceptación por el navegador o antivirus. La descarga 0.1.0 y su huella se mantienen sin modificar.

## Firma de autor

La firma pertinente para acreditar al editor de las DLL en Windows es **Authenticode**, con un certificado de firma de código emitido por una entidad de confianza. Requiere acceso legítimo al certificado y a su clave privada, normalmente custodiada por un dispositivo o servicio de firma. No se ha proporcionado ese certificado, por lo que estos paquetes **siguen sin firma Authenticode**.

Si se dispone de ese servicio, se firman las DLL con SHA-256 y sellado de tiempo antes de crear el ZIP, y se calculan las huellas después de firmar. El certificado identifica al editor y protege la integridad; no demuestra que el software sea inocuo ni obliga a Chrome o Defender a aceptarlo. Una firma autofirmada o una strong-name de .NET no sustituyen la confianza de Authenticode. No se deben compartir claves privadas en el chat ni subirlas al repositorio.

## Qué hacer ante el aviso

1. Copiar el texto exacto en las descargas de Chrome (`Ctrl+J`) y, si existe, el identificador del historial del antivirus. Una ruta, URL, PID o fecha de inicio de proceso no son el nombre de una amenaza.
2. Comparar el SHA-256 con el `.sha256` publicado solo si el archivo está accesible; no restaurarlo de cuarentena para comprobarlo. La coincidencia demuestra identidad con esa descarga, no ausencia de malware.
3. Identificar el proveedor real del bloqueo. Si es Microsoft Defender/SmartScreen, usar su [portal oficial de envío de archivos](https://www.microsoft.com/en-us/wdsi/filesubmission) y solicitar revisión del falso positivo. Para Chrome, consultar la [ayuda oficial de descargas bloqueadas](https://support.google.com/chrome/answer/6261569?hl=es). La revisión depende del servicio que haya emitido la alerta.
4. Publicar resultados verificables del análisis y, si se confirma un falso positivo, la respuesta del proveedor. No declarar el archivo «seguro» por una compilación exitosa, por una firma o por haber cambiado su icono, nombre, versión o compresión.

Los archivos no se han enviado automáticamente a servicios de análisis de terceros. Las dependencias de BepInEx/LethalLib se instalan por separado y quedan fuera del alcance de esta inspección de las dos DLL propias.

## Voz en 0.2.0

La nueva función de últimas palabras procesa voz propia mediante Dissonance, con consentimiento local desactivado por defecto y un máximo de cinco segundos. Comparte ese fragmento con los participantes conectados para reproducirlo tras reanimar. Se conserva en memoria hasta órbita/desconexión o revocación; nuestro mod no crea archivos de audio ni los envía a servicios de terceros. Consulta `docs/efectos-secundarios.md` para detalles, límites y pruebas pendientes. Las comprobaciones estáticas históricas de 0.1.0 no certifican esta nueva versión, que sigue sin firma Authenticode ni análisis antivirus actualizado.
