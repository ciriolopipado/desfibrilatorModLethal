# Desfibrilator — requisitos de primera versión

## Ajuste temporal para pruebas (0.1.2)

Por petición posterior del usuario, el nombre visible pasa a **Ciriolator** y el precio de tienda a **0 créditos**. El coste de 100 créditos descrito abajo corresponde al diseño inicial y queda suspendido durante estas pruebas.

## Requisitos acordados
- Mod de Lethal Company con BepInExPack 5.4.2305; se permiten dependencias adicionales.
- Objeto comprable en la tienda por 100 créditos; nombre: Desfibrilator.
- Solo puede reanimar a un jugador muerto y conectado con cadáver recuperable, accesible y próximo.
- Mantener E durante 5 segundos apuntando al cadáver, con el desfibrilador equipado en la mano activa.
- Cancelar al soltar E, alejarse, morir, dejar de equipar el objeto, perder el objetivo válido o terminar la ronda/cambiar la escena.
- Sin el desfibrilador activo, conservar la interacción normal de recoger cadáveres.
- Reutilizable. Tras una reanimación correcta, espera global de 90 segundos para toda la partida, independientemente de la unidad, usuario o cadáver.
- Mostrar cuenta atrás de indisponibilidad mientras está equipado y progreso durante la carga.
- Revivir junto al cadáver con exactamente 30 puntos de vida e inventario vacío.
- Conservar los objetos previamente soltados sin crearlos, duplicarlos ni destruirlos.
- Modelo original sencillo reconocible como desfibrilador; sonido de carga y descarga, con presentación visual del uso y animación de manos solicitada.
- Todos los participantes deberán instalar el mod y sus dependencias.

## Decisiones técnicas propuestas, pendientes de validación en juego
- El anfitrión valida el objetivo, duración, distancia y espera global.
- Resolver intentos simultáneos de forma que solo una reanimación pueda completarse antes de comenzar la espera global.
- Las cancelaciones no activan los 90 segundos de espera.
- La tecla se podrá cambiar; E será el valor por defecto.
- No reanimar jugadores desconectados ni cadáveres pertenecientes a rondas anteriores.
- La reanimación debe retirar el cadáver correspondiente y restaurar cámara, control y estado de espectador sin reanimar a otros jugadores.

## Perfil recibido
Lloreria LETHAL 2: 294 paquetes, 293 activados y 1 desactivado. Inventario completo en profile-inventory.json.
Dependencias presentes: Evaisa-LethalLib 1.2.0; Rune580-LethalCompany_InputUtils 0.7.13.
Prioridades de integración: MoreCompany 1.14.0, Poltergeist 1.2.12, HotbarPlus 1.8.6, ReviveDesyncPatch 1.0.0; revisar también efectos de salud/reanimación de Lategame_Upgrades y Emergency_Dice_Updated.
No se ha demostrado ningún conflicto: son candidatos para inspección y pruebas.

## Pendiente
- Versión objetivo confirmada por el usuario: Lethal Company v81. Falta validar binarios y comportamiento en esa instalación.
- Implementación, recursos Unity, compilación y pruebas dentro del juego, primero con dependencias mínimas y después con el perfil completo.
- Pruebas multijugador de anfitrión y cliente, más de cuatro jugadores, dos unidades y objetivos simultáneos, cancelaciones y nueva muerte tras reanimar.

Este documento recoge requisitos; no acredita una implementación ni compatibilidad probada.
