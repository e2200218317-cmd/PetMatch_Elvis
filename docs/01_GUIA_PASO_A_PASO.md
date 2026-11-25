# Guía de configuración y primera ejecución

## 1. Tu responsabilidad

El documento del equipo asigna a Elvis Moreira usuarios, autenticación y perfiles. Esta entrega implementa ese módulo e incorpora la baja de cuenta que solicitaste.

En el login se piden solamente **correo y contraseña**. Los demás datos se solicitan al registrarse. Se guarda fecha de nacimiento para calcular la edad correctamente con el tiempo; no se almacena una edad que quede desactualizada.

## 2. Abrir el proyecto en Visual Studio Code

1. Descomprime el ZIP.
2. En VS Code entra a **Archivo → Abrir carpeta** y selecciona `PetMatch_Elvis`.
3. Acepta las extensiones recomendadas si todavía no tienes .NET MAUI, C# y C# Dev Kit.
4. Abre la terminal integrada. Ejecuta:

```bash
dotnet --list-sdks
dotnet workload list
```

Debes tener un SDK .NET 10 y la carga de trabajo Android de MAUI. Si tu proyecto MAUI anterior ya se ejecuta en Android con .NET 10, conserva esa instalación.

Si falta la carga de trabajo:

```bash
dotnet workload install maui-android
```

Para Android necesitas además Android SDK y un JDK compatible con la versión de .NET para Android instalada. Usa el panel de configuración de la extensión MAUI para comprobar ambas rutas. En tu Mac Intel utiliza el emulador Android que ya tienes funcionando o un Android físico con depuración USB.

No necesitas instalar PostgreSQL local ni configurar FDW: esta app usa HTTPS hacia Supabase. Tampoco necesitas un paquete cliente Supabase de terceros: `SupabaseApi.cs` consume las API oficiales con `HttpClient`.

## 3. Crear el backend en Supabase

Usa un proyecto nuevo para esta práctica o el proyecto nuevo que acuerde el equipo. El SQL se diseñó para este esquema; no es una migración automática de tablas antiguas con nombres iguales.

1. Abre tu proyecto en Supabase.
2. En **SQL Editor**, crea una consulta.
3. Copia todo el contenido de `supabase/01_usuarios_perfiles.sql`.
4. Ejecuta **Run**. Debe finalizar sin errores.
5. En **Table Editor**, verifica `perfiles`, `perfiles_privados`, `contactos_perfil` y `cuentas_en_eliminacion`.
6. En **Storage**, verifica que existe `avatars`, privado, con máximo 5 MB por imagen.

`auth.users` ya pertenece a Supabase Auth. No debes crear una tabla de contraseñas ni insertar allí manualmente al usuario.

Si se ejecuta otra vez en el mismo esquema intacto, el SQL conserva los datos y vuelve a definir funciones y políticas del módulo. Revisa previamente otras políticas si el equipo ya añadió permisos: en PostgreSQL las políticas permisivas pueden combinarse y ampliar accesos.

## 4. Configurar Auth y los correos

En Authentication, habilita el proveedor **Email** y la confirmación del correo.

Configura una contraseña mínima de 8 caracteres y, donde estén disponibles, los requisitos de mayúsculas, minúsculas y números. La app aplica esas reglas y un máximo de 72 caracteres. Supabase debe seguir siendo quien valida y almacena las contraseñas.

En las plantillas de correo de Authentication:

| Plantilla | Archivo que debes copiar |
|---|---|
| Confirm sign up | `supabase/emails/confirmar_registro.html` |
| Reset password | `supabase/emails/recuperar_contrasena.html` |

Ambas usan `{{ .Token }}` para mostrar un código. Esta aplicación verifica el código dentro del móvil, por lo que no necesita un enlace profundo para esos dos recorridos.

**El envío de correo es un paso indispensable.** El proveedor de correo predeterminado de Supabase tiene restricciones de destinatarios y límites reducidos; para probar con correos de usuarios externos configura un SMTP propio o un Send Email Hook. No basta con escribir cualquier correo y asumir que el mensaje llegará. Revisa Authentication → Logs si no se entrega. No desactives la confirmación como solución a una prueba de recuperación que debe demostrarse con correo real.

Para las primeras pruebas usa una dirección autorizada por tu configuración y espera entre solicitudes de códigos. La app informa los límites de envío cuando el servidor los devuelve.

## 5. Conectar la app

Busca en la configuración del proyecto la **Project URL** y la **publishable key** (o la clave `anon` heredada).

Abre `PetMatch/Config/SupabaseConfig.cs` y reemplaza los valores:

```csharp
public const string Url = "https://TU_REFERENCIA.supabase.co";
public const string PublicKey = "sb_publishable_TU_VALOR";
```

La clave pública puede estar en la aplicación. La protección real está en Auth, las políticas RLS y las funciones del servidor. **No copies `service_role`, claves `sb_secret_...` ni la contraseña de PostgreSQL al código móvil.**

No necesitas compartir ninguna clave privada conmigo ni con el repositorio GitHub.

## 6. Desplegar la función de eliminación

El móvil no puede borrar usuarios de Auth con una clave pública. Por eso se incluye una función de servidor que valida la sesión y la contraseña, borra las imágenes y después elimina al usuario.

Opción con el panel:

1. Abre **Edge Functions** y crea una función llamada exactamente `delete-account`.
2. Copia `supabase/functions/delete-account/index.ts` en su `index.ts`.
3. Mantén la verificación JWT activada y despliega.
4. Verifica que el entorno de la función dispone de `SUPABASE_URL`, `SUPABASE_ANON_KEY` y `SUPABASE_SERVICE_ROLE_KEY`. Se usan únicamente en el servidor. Si tu proyecto no las expone como variables integradas, configúralas en Secrets de la función, nunca en el móvil.

Opción con Supabase CLI, instalada según su documentación oficial:

```bash
supabase login
supabase link --project-ref TU_REFERENCIA
supabase functions deploy delete-account
```

Ejecuta esos comandos desde la carpeta `PetMatch_Elvis`. Si tu versión de CLI solicita inicializar el directorio, conserva el archivo `supabase/config.toml` y el código de la función al hacerlo.

Si la función devuelve 401 antes de ejecutar su código, revisa sus logs y la compatibilidad de verificación JWT con la configuración de claves del proyecto; no desactives la validación por ensayo y error. La función también verifica al usuario contra Auth en su propio código.

Una eliminación que falle durante la limpieza queda marcada como pendiente y se puede reintentar. No se anuncia éxito antes de que Auth confirme el borrado. En el proyecto integrado, todas las políticas de escritura deben verificar `cuenta_disponible(auth.uid())` para impedir nuevas publicaciones durante esa limpieza.

## 7. Compilar y ejecutar

Desde la carpeta `PetMatch_Elvis`:

```bash
dotnet restore PetMatch/PetMatch.csproj
dotnet build PetMatch/PetMatch.csproj -f net10.0-android -m:1
```

En VS Code selecciona `PetMatch` como proyecto de inicio y tu emulador Android como dispositivo. Inicia la depuración con F5 desde la extensión MAUI.

También puedes ejecutar desde terminal con el emulador ya encendido:

```bash
dotnet build PetMatch/PetMatch.csproj -t:Run -f net10.0-android -m:1
```

Si tu equipo requiere especificar rutas, añade los parámetros con los valores reales de tu Mac:

```bash
dotnet build PetMatch/PetMatch.csproj -f net10.0-android -m:1 \
  -p:JavaSdkDirectory="RUTA_REAL_DEL_JDK" \
  -p:AndroidSdkDirectory="RUTA_REAL_DEL_ANDROID_SDK"
```

`EmbedAssembliesIntoApk=true` ya está incluido para evitar el problema de despliegue rápido que puede provocar XA0129. No cambies la aplicación a `net8.0` o `net9.0` sin adaptar y probar el proyecto.

## 8. Primera prueba completa

1. Abre la app: verás el inicio y los botones para acceder o registrarte.
2. Entra en **Crear una cuenta**.
3. Escribe correo, contraseña y confirmación; completa los datos y selecciona una imagen JPG/PNG.
4. Usa fecha de nacimiento correspondiente a una persona mayor de 18 años. La cédula se valida como 10 dígitos y única; **no se consulta al Registro Civil ni se verifica oficialmente la identidad**.
5. Pulsa **Crear mi cuenta**. Copia el código del correo y confirma.
6. La app sube la foto, guarda el perfil completo y abre **Mi perfil**.
7. Edita dirección o celular y comprueba que el cambio permanece al volver a entrar.
8. Cierra y abre la app. La sesión se recupera; pulsa **Ir a mi perfil**.
9. Cierra sesión. Prueba **Recuperar contraseña**, verifica el código y cambia la clave.
10. Inicia sesión con la clave nueva. Para comprobar la baja, utiliza exclusivamente una cuenta creada para esa prueba.

Si cierras la app después de solicitar el correo y antes de completar el registro, confirma desde **Confirmar mi correo** o inicia sesión después de confirmar. El formulario de completar perfil aparecerá de nuevo. Los datos personales y la foto que aún no se enviaron no se guardan sin cifrar para retomarlos.

El registro de Auth y la carga de imagen no forman una única transacción de red. Si se interrumpe, puede existir una cuenta aún sin perfil; el flujo de completar perfil permite terminarla. El guardado de las tres tablas del perfil sí es una transacción atómica.

## 9. Errores habituales

| Mensaje o situación | Revisión |
|---|---|
| URL/clave sin configurar | Cambia `SupabaseConfig.cs` |
| Correo sin confirmar | Usa el código y la pantalla de confirmación |
| Correo no llega | Revisa SMTP, destinatarios permitidos, spam, límites y logs |
| Código vencido | Solicita uno nuevo y usa el último recibido |
| Error 404 al eliminar | Despliega `delete-account` con ese nombre |
| Error de permisos/RLS | Ejecuta el SQL completo y comprueba que el usuario inició sesión |
| Foto rechazada | JPG o PNG, máximo 5 MB; otra carpeta no está permitida |
| Cédula ya registrada | Prueba con tu propia cédula o con una cuenta de prueba diferente |
| Android SDK/JDK no encontrado | Configura las rutas en la extensión MAUI |
| Inicio carga pero no hay mascotas | El catálogo real es el módulo de David, pendiente de integración |

## Fuentes técnicas oficiales consultadas

- [.NET MAUI y Visual Studio Code](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation?view=net-maui-10.0&tabs=visual-studio-code)
- [Almacenamiento seguro en MAUI](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage?view=net-maui-10.0)
- [API oficial de Supabase Auth](https://github.com/supabase/auth)
- [Plantillas de correo](https://supabase.com/docs/guides/auth/auth-email-templates)
- [SMTP de Supabase](https://supabase.com/docs/guides/auth/auth-smtp)
- [Row Level Security](https://supabase.com/docs/guides/database/postgres/row-level-security)
- [Permisos de Storage](https://supabase.com/docs/guides/storage/security/access-control)
- [Gestión y eliminación de usuarios](https://supabase.com/docs/guides/auth/managing-user-data)

Consulta: 7 de noviembre de 2026, fecha local de Ecuador. Los nombres de menús pueden variar con futuras actualizaciones.
