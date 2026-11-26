-- PETMATCH / Elvis Moreira. Ejecutar en SQL Editor de un proyecto Supabase nuevo.
-- Esquema de usuarios únicamente. No crea catálogo, mascotas, solicitudes ni notificaciones.
begin;

create table if not exists public.perfiles (
  id uuid primary key references auth.users(id) on delete cascade,
  nombres text not null check (char_length(btrim(nombres)) between 2 and 80),
  apellidos text not null check (char_length(btrim(apellidos)) between 2 and 80),
  ciudad text not null check (char_length(btrim(ciudad)) between 2 and 100),
  avatar_path text,
  creado_en timestamptz not null default now(),
  actualizado_en timestamptz not null default now()
);

create table if not exists public.perfiles_privados (
  usuario_id uuid primary key references public.perfiles(id) on delete cascade,
  fecha_nacimiento date not null,
  cedula text not null unique check (cedula ~ '^[0-9]{10}$'),
  eliminando boolean not null default false,
  constraint mayor_de_edad check (fecha_nacimiento >= date '1900-01-01'
    and fecha_nacimiento <= (current_date - interval '18 years')::date)
);

create table if not exists public.contactos_perfil (
  usuario_id uuid primary key references public.perfiles(id) on delete cascade,
  direccion text not null check (char_length(btrim(direccion)) between 5 and 250),
  celular text not null check (celular ~ '^\+5939[0-9]{8}$')
);

-- Marca interna también para cuentas que todavía no completaron el perfil.
create table if not exists public.cuentas_en_eliminacion (
  usuario_id uuid primary key references auth.users(id) on delete cascade
);
alter table public.cuentas_en_eliminacion enable row level security;
revoke all on public.cuentas_en_eliminacion from anon, authenticated;
grant all on public.cuentas_en_eliminacion to service_role;

-- Los ayudantes no exponen información personal. Evitan recursión de políticas RLS.
create or replace function public.cuenta_disponible(p_usuario uuid)
returns boolean language sql stable security definer set search_path = '' as $$
  select exists(select 1 from auth.users where id = p_usuario)
    and not exists(select 1 from public.cuentas_en_eliminacion where usuario_id = p_usuario);
$$;
revoke all on function public.cuenta_disponible(uuid) from public;
grant execute on function public.cuenta_disponible(uuid) to anon, authenticated, service_role;

alter table public.perfiles enable row level security;
alter table public.perfiles_privados enable row level security;
alter table public.contactos_perfil enable row level security;

revoke all on public.perfiles, public.perfiles_privados, public.contactos_perfil from anon, authenticated;
grant select on public.perfiles to anon, authenticated;
grant select on public.perfiles_privados, public.contactos_perfil to authenticated;
grant all on public.perfiles, public.perfiles_privados, public.contactos_perfil to service_role;

drop policy if exists perfiles_lectura on public.perfiles;
create policy perfiles_lectura on public.perfiles for select to anon, authenticated
using (public.cuenta_disponible(id));
drop policy if exists privados_solo_titular on public.perfiles_privados;
create policy privados_solo_titular on public.perfiles_privados for select to authenticated
using (usuario_id = (select auth.uid()) and public.cuenta_disponible((select auth.uid())));
drop policy if exists contactos_usuarios_registrados on public.contactos_perfil;
create policy contactos_usuarios_registrados on public.contactos_perfil for select to authenticated
using (public.cuenta_disponible((select auth.uid())) and public.cuenta_disponible(usuario_id));

-- No hay INSERT/UPDATE/DELETE directos para clientes: se guarda todo de forma atómica mediante RPC.
create or replace function public.guardar_perfil(
  p_nombres text, p_apellidos text, p_ciudad text, p_direccion text,
  p_celular text, p_cedula text, p_fecha_nacimiento date, p_avatar_path text)
returns void language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid(); v_anterior public.perfiles_privados%rowtype;
begin
  if v_id is null or not public.cuenta_disponible(v_id) then
    raise exception 'PETMATCH: Inicia sesión con una cuenta activa.';
  end if;
  -- El bloqueo evita carreras entre una edición y el inicio de eliminación.
  perform pg_advisory_xact_lock(hashtextextended(v_id::text, 0));
  if not public.cuenta_disponible(v_id) then raise exception 'PETMATCH: La cuenta se está eliminando.'; end if;
  if p_nombres is null or char_length(btrim(p_nombres)) not between 2 and 80
     or p_apellidos is null or char_length(btrim(p_apellidos)) not between 2 and 80 then
    raise exception 'PETMATCH: Completa nombres y apellidos.';
  end if;
  if p_fecha_nacimiento is null or p_fecha_nacimiento < date '1900-01-01'
     or p_fecha_nacimiento > (current_date - interval '18 years')::date then
    raise exception 'PETMATCH: Debes tener al menos 18 años.';
  end if;
  if p_cedula is null or p_cedula !~ '^[0-9]{10}$' then
    raise exception 'PETMATCH: La cédula debe tener 10 dígitos.';
  end if;
  if p_ciudad is null or char_length(btrim(p_ciudad)) not between 2 and 100
     or p_direccion is null or char_length(btrim(p_direccion)) not between 5 and 250 then
    raise exception 'PETMATCH: Completa ciudad y dirección.';
  end if;
  if p_celular is null or p_celular !~ '^\+5939[0-9]{8}$' then
    raise exception 'PETMATCH: El celular debe usar el formato +5939XXXXXXXX.';
  end if;
  if p_avatar_path is not null and (split_part(p_avatar_path, '/', 1) <> v_id::text
     or not exists(select 1 from storage.objects where bucket_id = 'avatars' and name = p_avatar_path)) then
    raise exception 'PETMATCH: La foto de perfil no es válida.';
  end if;
  select * into v_anterior from public.perfiles_privados where usuario_id = v_id;
  if found and (v_anterior.cedula <> p_cedula or v_anterior.fecha_nacimiento <> p_fecha_nacimiento) then
    raise exception 'PETMATCH: La cédula y la fecha de nacimiento no se pueden cambiar desde la aplicación.';
  end if;
  insert into public.perfiles(id, nombres, apellidos, ciudad, avatar_path)
  values(v_id, btrim(p_nombres), btrim(p_apellidos), btrim(p_ciudad), p_avatar_path)
  on conflict(id) do update set nombres = excluded.nombres, apellidos = excluded.apellidos,
    ciudad = excluded.ciudad, avatar_path = excluded.avatar_path, actualizado_en = now();
  insert into public.perfiles_privados(usuario_id, fecha_nacimiento, cedula)
  values(v_id, p_fecha_nacimiento, p_cedula) on conflict(usuario_id) do nothing;
  insert into public.contactos_perfil(usuario_id, direccion, celular)
  values(v_id, btrim(p_direccion), p_celular)
  on conflict(usuario_id) do update set direccion = excluded.direccion, celular = excluded.celular;
end;
$$;
revoke all on function public.guardar_perfil(text,text,text,text,text,text,date,text) from public;
grant execute on function public.guardar_perfil(text,text,text,text,text,text,date,text) to authenticated;

create or replace function public.mi_perfil()
returns table(id uuid, email text, nombres text, apellidos text, ciudad text, avatar_path text,
  fecha_nacimiento date, cedula text, direccion text, celular text, eliminando boolean)
language sql stable security definer set search_path = '' as $$
  select p.id, u.email::text, p.nombres, p.apellidos, p.ciudad, p.avatar_path,
    s.fecha_nacimiento, s.cedula, c.direccion, c.celular, s.eliminando
  from public.perfiles p join auth.users u on u.id = p.id
    join public.perfiles_privados s on s.usuario_id = p.id
    join public.contactos_perfil c on c.usuario_id = p.id
  where p.id = auth.uid();
$$;
revoke all on function public.mi_perfil() from public;
grant execute on function public.mi_perfil() to authenticated;

-- Perfil de otra persona para integrar con el detalle de mascota y solicitudes.
-- Nunca devuelve cédula, nacimiento ni correo. Contactos visibles solo con login.
create or replace function public.ver_perfil_contacto(p_usuario uuid)
returns table(id uuid, nombres text, apellidos text, ciudad text, avatar_path text, direccion text, celular text)
language sql stable security definer set search_path = '' as $$
  select p.id, p.nombres, p.apellidos, p.ciudad, p.avatar_path, c.direccion, c.celular
  from public.perfiles p join public.contactos_perfil c on c.usuario_id = p.id
  where p.id = p_usuario and public.cuenta_disponible(auth.uid()) and public.cuenta_disponible(p.id);
$$;
revoke all on function public.ver_perfil_contacto(uuid) from public;
grant execute on function public.ver_perfil_contacto(uuid) to authenticated;

create or replace function public.iniciar_eliminacion(p_usuario uuid)
returns void language plpgsql security definer set search_path = '' as $$
begin
  perform pg_advisory_xact_lock(hashtextextended(p_usuario::text, 0));
  insert into public.cuentas_en_eliminacion(usuario_id) values(p_usuario) on conflict do nothing;
  update public.perfiles_privados set eliminando = true where usuario_id = p_usuario;
end;
$$;
revoke all on function public.iniciar_eliminacion(uuid) from public, anon, authenticated;
grant execute on function public.iniciar_eliminacion(uuid) to service_role;

insert into storage.buckets(id, name, public, file_size_limit, allowed_mime_types)
values ('avatars', 'avatars', false, 5242880, array['image/jpeg','image/png'])
on conflict(id) do update set public = false, file_size_limit = 5242880,
  allowed_mime_types = array['image/jpeg','image/png'];

drop policy if exists avatars_leer on storage.objects;
create policy avatars_leer on storage.objects for select to authenticated using (
  bucket_id = 'avatars' and public.cuenta_disponible((select auth.uid())) and
  ((storage.foldername(name))[1] = (select auth.uid())::text
    or exists(select 1 from public.perfiles p where p.avatar_path = name and public.cuenta_disponible(p.id)))
);
drop policy if exists avatars_crear on storage.objects;
create policy avatars_crear on storage.objects for insert to authenticated with check (
  bucket_id = 'avatars' and (storage.foldername(name))[1] = (select auth.uid())::text
  and public.cuenta_disponible((select auth.uid()))
);
drop policy if exists avatars_eliminar on storage.objects;
create policy avatars_eliminar on storage.objects for delete to authenticated using (
  bucket_id = 'avatars' and (storage.foldername(name))[1] = (select auth.uid())::text
  and public.cuenta_disponible((select auth.uid()))
  and not exists(select 1 from public.perfiles p where p.avatar_path = name)
);
-- Fotos inmutables: no UPDATE/upsert. Se sube un nombre UUID nuevo y luego se cambia la referencia.
commit;
