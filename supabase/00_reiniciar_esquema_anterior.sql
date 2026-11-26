-- PETMATCH: reinicio del esquema anterior sin conservar sus datos.
-- Ejecutar una sola vez en SQL Editor ANTES de 01_usuarios_perfiles.sql.
-- Elimina únicamente las tablas, funciones y tipos públicos identificados como PetMatch.
-- No elimina cuentas de Supabase Auth ni modifica el esquema storage.
begin;

-- Borrar primero las tablas antiguas. CASCADE quita sus claves foráneas,
-- políticas, índices y triggers dependientes.
drop table if exists public.fotos_mascotas cascade;
drop table if exists public.solicitudes cascade;
drop table if exists public.notificaciones cascade;
drop table if exists public.mascotas cascade;
drop table if exists public.perfiles cascade;
drop table if exists public.razas cascade;
drop table if exists public.especies cascade;
drop table if exists public.estados_solicitud cascade;

-- Funciones de negocio del esquema antiguo.
drop function if exists public.cancelar_solicitud(uuid);
drop function if exists public.crear_solicitud(uuid, text);
drop function if exists public.marcar_notificacion_leida(uuid);
drop function if exists public.responder_solicitud(uuid, boolean);
drop function if exists public.generar_notificacion_solicitud() cascade;
-- Elimina también el trigger antiguo conectado a auth.users, si existe.
drop function if exists public.crear_perfil_usuario() cascade;

-- El modelo anterior utilizaba enums en columnas de mascotas.
-- Después de retirar las tablas se pueden quitar para que no queden tipos obsoletos.
drop type if exists public.estado_mascota cascade;
drop type if exists public.sexo_mascota cascade;
drop type if exists public.tamano_mascota cascade;

commit;
