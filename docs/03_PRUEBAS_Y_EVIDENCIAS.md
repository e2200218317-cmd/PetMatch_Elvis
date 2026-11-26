# Pruebas y evidencias de la primera parte

## Verificación efectuada al preparar esta entrega

| Comprobación | Resultado |
|---|---|
| Compilación de `PetMatch.Core` con .NET 10 | Correcta |
| Compilación de los archivos C# de interfaz contra MAUI 10, destino neutral .NET | Correcta; cero advertencias y errores |
| Pruebas C# de validaciones, sesión, concurrencia y recuperación con HTTP simulado | 23 comprobaciones correctas |
| SQL ejecutado en PostgreSQL embebido (PGlite), con esquemas Auth/Storage simulados | 24 comprobaciones correctas |
| Compilación Android completa/APK | Pendiente: el entorno no tiene SDK/JDK Android completos y la instalación automática no pudo terminarlos |
| Vista y navegación en un emulador real | Pendiente |
| Correo, Supabase alojado, Storage y Edge Function desplegada | Pendiente: requieren configurar el proyecto real |

La compilación neutral comprueba las API y los tipos C# de las vistas; no reemplaza compilar el destino Android. Las pruebas de PostgreSQL comprueban SQL y RLS; no reemplazan probar los servicios de correo, Storage ni la función Deno alojada.

## Repetir las pruebas automáticas

Desde la raíz de la entrega:

```bash
dotnet run --project tests/PetMatch.Tests
```

Para las pruebas de base de datos necesitas Node.js y el paquete de pruebas PGlite:

```bash
npm install --no-save @electric-sql/pglite
node tests/database.test.mjs
```

Estas pruebas no se conectan a tu Supabase real ni modifican tus cuentas. Comprueban, entre otros casos: una persona que cumple 18 hoy, una menor por un día, foto obligatoria, exceso de tamaño, cédula duplicada, reversión de un perfil parcial, acceso de visitantes, aislamiento de cédulas entre usuarios, protección de carpetas de fotos y bloqueo durante eliminación.

## Pruebas manuales antes de entregar al docente

| Caso | Resultado esperado |
|---|---|
| Inicio como visitante | Inicio visible sin datos privados |
| Login con correo mal escrito | Mensaje de correo inválido |
| Contraseña incorrecta | No permite entrar |
| Registro con campo vacío | Identifica el dato faltante |
| Persona menor de edad | No se puede registrar con esa fecha; servidor también la rechaza |
| Cédula vacía o distinta de 10 dígitos | Registro detenido |
| Cédula ya usada | No crea un segundo perfil ni deja tablas a medias |
| Foto ausente, formato ajeno o mayor de 5 MB | Selección/guardado rechazado |
| Registro con correo confirmado | Foto y perfil guardados, abre Mi perfil |
| Cerrar app antes de confirmar | Al retomar puede confirmar y completar datos nuevamente |
| Editar nombres, apellidos, ciudad, dirección, celular o foto | Cambios persistentes |
| Intentar modificar cédula/nacimiento | Controles bloqueados; RPC también rechaza el cambio |
| Cerrar y reabrir la app | Sesión restaurada si sigue siendo válida |
| Token vencido | Se renueva antes de acceder a los datos |
| Sin Internet | Error comprensible; no muestra falsos mensajes de guardado |
| Cerrar sesión | No vuelve al perfil mediante el botón Atrás |
| Recuperar contraseña | Código real recibido, contraseña nueva aceptada, contraseña anterior rechazada |
| Código errado/vencido | No modifica contraseña |
| Cuenta A intenta leer cédula de B | Cero filas de B o permiso denegado |
| A intenta editar B o cargar fotos en carpeta de B | Operación rechazada por el backend |
| Eliminar cuenta con contraseña errónea | No elimina la cuenta |
| Eliminar cuenta de prueba | Fotos y usuario eliminados; login posterior falla |
| Eliminación interrumpida | No anuncia éxito; permite reintentar y no admite nuevas escrituras |

Usa dos cuentas de prueba para verificar aislamiento. No pruebes RLS solamente desde SQL Editor con el rol administrador: ese rol puede saltarse las restricciones. Las pruebas deben ejecutarse con los JWT reales de las cuentas A y B o con un entorno de pruebas que simule sus roles.

## Evidencias que puedes capturar

1. Pantalla de login con el diseño blanco, negro y celeste.
2. Formulario de registro completo, ocultando cédula y teléfono reales en la evidencia que compartas.
3. Validación de campo obligatorio o contraseña diferente.
4. Correo de confirmación y pantalla de código; no muestres un código vigente.
5. Perfil después de iniciar sesión.
6. Perfil editado y cambio comprobado después de volver a abrir la app.
7. Recuperación completada e inicio con contraseña nueva.
8. Tablas relacionadas en Supabase, sin contraseñas ni tokens.
9. Prueba de acceso denegado a datos privados de otro usuario.
10. Eliminación de una cuenta de prueba y desaparición de sus datos.

## Explicación breve para tu exposición

Mi módulo permite registrar usuarios mayores de 18 años, confirmar su correo, iniciar sesión, recuperar la contraseña y administrar su perfil. Supabase Auth gestiona las credenciales; PostgreSQL guarda los datos del perfil y Storage guarda las fotografías. Las tablas se relacionan mediante el UUID del usuario. Las políticas RLS protegen la información privada, y la sesión se conserva de forma segura en el dispositivo. Dejé un mecanismo común para que los módulos de catálogo y publicaciones reconozcan al usuario autenticado.

## Prueba de carga

`tests/PetMatch.Carga` simula usuarios simultáneos que abren el catálogo público usando el mismo cliente de la app (`SupabaseApi`).

```
dotnet run --project tests/PetMatch.Carga -- <url_supabase> <clave_publica> [usuarios] [rondas]
```

Resultado del 25/11/2026 contra el proyecto de Supabase del equipo (plan gratuito), desde una red doméstica:

| Usuarios simultáneos | Rondas | Solicitudes | Errores | Promedio | p95 |
|---|---|---|---|---|---|
| 50 | 3 | 150 | 0 | 402 ms | 1096 ms |
| 100 | 3 | 300 | 0 | 262 ms | 498 ms |

La primera ronda es más lenta porque incluye abrir las conexiones seguras (TLS); las siguientes reutilizan la conexión y bajan a unos 200 ms.

## Prueba de conectividad

Con el teléfono en modo avión, abrir cada pantalla y anotar el resultado. La app no debe cerrarse y debe mostrar un mensaje claro; al reconectar, el botón de reintento o volver a abrir la pantalla debe cargar los datos.

| Pantalla | Sin Internet | Al reconectar |
|---|---|---|
| Inicio (arranque) | | |
| Catálogo | | |
| Detalle de mascota | | |
| Iniciar sesión | | |
| Mi perfil | | |
| Publicar (fotos, cámara y ubicación) | | |
| Mis publicaciones | | |
| Solicitudes recibidas | | |
| Mis solicitudes | | |
| Notificaciones | | |

## Sensores y servicio externo

| Requisito | Dónde | Cómo probar |
|---|---|---|
| Cámara | Publicar → «Tomar foto» | Tomar una foto, ver la miniatura y publicar |
| GPS | Publicar → «Usar mi ubicación actual» | Aceptar el permiso; el campo Ubicación se llena con ciudad y sector |
| Servicio web externo | OpenStreetMap Nominatim (`PetMatch.Core/PlaceLookup.cs`) | Convierte las coordenadas del GPS en una dirección legible |
