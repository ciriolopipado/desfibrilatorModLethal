# Ciriolator 1.0.0 — primera versión estable

Esta publicación fija el precio oficial en **50 créditos** y se designa como primera versión estable del proyecto a petición del usuario. Conserva la reanimación y los efectos de 0.3.0, sin cambios de protocolo ni nuevas dependencias.

## Precio y actualización

- Precio de compra fijo: **50 créditos por unidad**, aplicado al objeto y a su registro en LethalLib. Los descuentos nativos de tienda están desactivados para este objeto. No hay opción de precio en la configuración del mod.
- Importar `ciriolopipado-Desfibrilator-1.0.0.zip` desde **Settings → Profile → Import local mod**, sin descomprimirlo. Si pide datos: autor `ciriolopipado`, nombre `Desfibrilator`, versión `1.0.0`.
- Todos los participantes deben actualizar a 1.0.0 y reiniciar. Retirar instalaciones manuales duplicadas. No hace falta borrar la configuración existente.
- El objeto continúa llamándose **Ciriolator** y se conserva la identidad interna del paquete y del plugin para actualizar instalaciones existentes.

## Comportamiento incluido

Carga de cinco segundos junto a un cadáver recuperable, reanimación con 30 puntos de vida e inventario vacío, sin duplicar ni eliminar los objetos caídos, y espera global de 90 segundos.

Los ecos y las descargas se repiten a intervalos independientes de 15–30 segundos. Las descargas giran la cámara 90–180° con intensidad predeterminada y tienen un 50 % de probabilidad de solicitar caminar durante 0,75 segundos y un salto normal. Las chispas aparecen solo durante la descarga, durante 0,75 segundos, sobre el torso del resucitado. Los compañeros las ven sobre ese cuerpo; no se añade una superposición a sus pantallas. Al llegar a órbita se eliminan los efectos y sus datos.

## Comprobación y alcance de la validación

La compilación usa las referencias públicas de v81 y se ejecutan 48 pruebas de lógica, junto con la inspección del paquete. El usuario comunicó buen funcionamiento de 0.2.2. **No se ejecuta Lethal Company en este entorno**: el nombre estable es la designación de la publicación solicitada y no una certificación de todos los efectos o de los mods del perfil.

Al comprobar esta versión en el juego:

1. Confirmar `Ciriolator 1.0.0 loaded` en ambos registros y un precio de 50 en el terminal.
2. Con 49 créditos, la compra debe rechazarse; con 50, comprar una unidad debe dejar el saldo a 0. Dos unidades deben costar 100. Comprobar que el precio sigue siendo 50 en jornadas con rebajas.
3. Reanimar al anfitrión y al cliente y comprobar pasos, animaciones, voz y los efectos descritos en [pruebas.md](pruebas.md).
4. Comprobar que las chispas desaparecen entre descargas y que llegar a órbita borra todos los efectos, sin reactivación en la siguiente expedición salvo otra reanimación.

La animación propia de manos colocando las palas sobre el pecho sigue pendiente; se conserva la pose básica y el movimiento procedural de palas. El paquete no está firmado con Authenticode ni analizado por un antivirus en este entorno; véase [SECURITY.md](../SECURITY.md).
