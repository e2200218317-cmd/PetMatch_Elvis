# PETMATCH · Módulo de Elvis Moreira

Proyecto académico en .NET MAUI 10 para Android, con Supabase Auth, PostgreSQL y Storage.


## que contiene este modulo

| Función | Implementación incluida |
|---|---|
| Inicio de sesión | Correo y contraseña; mensajes de error |
| Registro | Nombres, apellidos, nacimiento (18+), dirección, ciudad, celular, cédula y foto |
| Confirmación del correo | Código enviado por Supabase Auth |
| Recuperación | Envío de código, verificación y guardado de nueva contraseña |
| Perfil | Consulta, foto y edición de campos permitidos |
| Sesión | SecureStorage, restauración y renovación del token |
| Acceso protegido | Método común para abrir detalle/publicación después del login |
| Eliminar cuenta | Reautenticación, limpieza de fotos y borrado de Auth mediante Edge Function |
| Base de datos | Tablas, relaciones, RPC, validación en servidor y políticas RLS |
| Integración | Botones y contrato para los módulos de David y Dylan |

El catálogo, scroll infinito, detalle de mascota, publicaciones, intereses, solicitudes, notificaciones y vencimiento a los 10 días **corresponden a los otros módulos**. Sus botones abren pantallas identificadas como pendientes de integración; no guardan mascotas ni simulan que estas funciones están terminadas.

## Archivos principales

| Ruta | Para qué sirve |
|---|---|
| `PetMatch.slnx` | Solución que abre VS Code |
| `PetMatch/` | Aplicación móvil MAUI y pantallas |
| `PetMatch/Views/` | Vistas nativas construidas en C# |
| `PetMatch/Config/SupabaseConfig.cs` | URL del proyecto y clave pública |
| `PetMatch.Core/` | Autenticación, validaciones y llamadas HTTP |
| `supabase/01_usuarios_perfiles.sql` | Ejecutar primero en SQL Editor |
| `supabase/02_catalogo_mascotas.sql` | Ejecutar después en el mismo proyecto para habilitar el catálogo de Inicio |
| `supabase/functions/delete-account/index.ts` | Función de servidor para eliminar la cuenta |
| `supabase/emails/` | Plantillas de correo con código |
| `docs/01_GUIA_PASO_A_PASO.md` | Instalación, configuración y primera prueba |
| `docs/02_INTEGRACION_EQUIPO.md` | Relaciones y uso desde otros módulos |
| `docs/03_PRUEBAS_Y_EVIDENCIAS.md` | Comprobaciones y capturas para la entrega |

Las vistas se escribieron con controles nativos MAUI en archivos `.cs`, una opción válida de MAUI. No son páginas web ni requieren XAML. El diseño común está en `Views/Ui.cs`: blanco, negro, celeste y bordes redondeados.

**proyecto Supabase y ejecutar las pruebas ejecutadas correctamente .**
