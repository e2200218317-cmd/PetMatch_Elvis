-- PETMATCH / Dylan Jaramillo. Ejecutar después de 03_publicaciones.sql.
-- Solicitudes de adopción, notificaciones internas y vencimiento de publicaciones a los 10 días.
begin;

create table if not exists public.solicitudes (
  id uuid primary key default gen_random_uuid(),
  mascota_id uuid not null references public.mascotas(id) on delete cascade,
  solicitante_id uuid not null references public.perfiles(id) on delete cascade,
  mensaje text check (length(mensaje) <= 500),
  estado text not null default 'Pendiente' check (estado in ('Pendiente', 'Aceptada', 'Rechazada')),
  creado_en timestamptz not null default now(),
  actualizado_en timestamptz not null default now(),
  unique (mascota_id, solicitante_id)
);
create index if not exists solicitudes_mascota_idx on public.solicitudes (mascota_id);
create index if not exists solicitudes_solicitante_idx on public.solicitudes (solicitante_id);

create table if not exists public.notificaciones (
  id uuid primary key default gen_random_uuid(),
  destinatario_id uuid not null references public.perfiles(id) on delete cascade,
  mascota_id uuid references public.mascotas(id) on delete set null,
  mensaje text not null,
  leida boolean not null default false,
  creado_en timestamptz not null default now()
);
create index if not exists notificaciones_destinatario_idx on public.notificaciones (destinatario_id, creado_en desc);

alter table public.solicitudes enable row level security;
alter table public.notificaciones enable row level security;
revoke all on public.solicitudes, public.notificaciones from anon, authenticated;
grant select on public.solicitudes, public.notificaciones to authenticated;
grant all on public.solicitudes, public.notificaciones to service_role;

-- El solicitante ve sus solicitudes; el responsable ve las de sus mascotas.
drop policy if exists solicitudes_lectura on public.solicitudes;
create policy solicitudes_lectura on public.solicitudes for select to authenticated
using (public.cuenta_disponible((select auth.uid()))
  and (solicitante_id = (select auth.uid()) or public.es_responsable(mascota_id)));
drop policy if exists notificaciones_propias on public.notificaciones;
create policy notificaciones_propias on public.notificaciones for select to authenticated
using (destinatario_id = (select auth.uid()));

create or replace function public.nombre_mascota(p_mascota uuid)
returns text language sql stable security definer set search_path = '' as $$
  select coalesce(nombre, especie) from public.mascotas where id = p_mascota;
$$;
revoke all on function public.nombre_mascota(uuid) from public;

-- Para el módulo de David: enviar una solicitud desde el detalle de la mascota.
create or replace function public.enviar_solicitud(p_mascota uuid, p_mensaje text default null)
returns uuid language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid(); v_solicitud uuid; v_mascota public.mascotas%rowtype;
begin
  if v_id is null or not public.cuenta_disponible(v_id)
     or not exists(select 1 from public.perfiles where id = v_id) then
    raise exception 'PETMATCH: Inicia sesión y completa tu perfil.';
  end if;
  select * into v_mascota from public.mascotas where id = p_mascota for update;
  if not found or not v_mascota.disponible or v_mascota.vence_en <= now()
     or not public.cuenta_disponible(v_mascota.propietario_id) then
    raise exception 'PETMATCH: Esta mascota ya no está disponible.';
  end if;
  if v_mascota.propietario_id = v_id then
    raise exception 'PETMATCH: No puedes solicitar tu propia mascota.';
  end if;
  if coalesce(char_length(btrim(p_mensaje)), 0) > 500 then
    raise exception 'PETMATCH: El mensaje admite máximo 500 caracteres.';
  end if;
  if exists(select 1 from public.solicitudes where mascota_id = p_mascota and solicitante_id = v_id) then
    raise exception 'PETMATCH: Ya enviaste una solicitud para esta mascota.';
  end if;
  insert into public.solicitudes(mascota_id, solicitante_id, mensaje)
  values (p_mascota, v_id, nullif(btrim(p_mensaje), '')) returning id into v_solicitud;
  return v_solicitud;
end;
$$;
revoke all on function public.enviar_solicitud(uuid, text) from public;
grant execute on function public.enviar_solicitud(uuid, text) to authenticated;

create or replace function public.solicitudes_recibidas()
returns table(id uuid, mascota_id uuid, mascota text, estado text, mensaje text, creado_en timestamptz,
  solicitante_id uuid, nombres text, apellidos text, ciudad text, celular text)
language sql stable security definer set search_path = '' as $$
  select s.id, m.id, coalesce(m.nombre, m.especie), s.estado, s.mensaje, s.creado_en,
    p.id, p.nombres, p.apellidos, p.ciudad, c.celular
  from public.solicitudes s join public.mascotas m on m.id = s.mascota_id
    join public.perfiles p on p.id = s.solicitante_id
    join public.contactos_perfil c on c.usuario_id = p.id
  where m.propietario_id = auth.uid() and public.cuenta_disponible(auth.uid())
    and public.cuenta_disponible(p.id)
  order by (s.estado = 'Pendiente') desc, s.creado_en desc;
$$;
revoke all on function public.solicitudes_recibidas() from public;
grant execute on function public.solicitudes_recibidas() to authenticated;

-- Al aceptar, la mascota sale del catálogo y las demás solicitudes pendientes se rechazan.
create or replace function public.responder_solicitud(p_solicitud uuid, p_aceptar boolean)
returns void language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid(); v_solicitud public.solicitudes%rowtype; v_mascota public.mascotas%rowtype;
begin
  if v_id is null or not public.cuenta_disponible(v_id) then
    raise exception 'PETMATCH: Inicia sesión con una cuenta activa.';
  end if;
  select * into v_solicitud from public.solicitudes where id = p_solicitud;
  if found then select * into v_mascota from public.mascotas where id = v_solicitud.mascota_id for update; end if;
  if not found or v_mascota.propietario_id <> v_id then
    raise exception 'PETMATCH: La solicitud no existe o no pertenece a tus publicaciones.';
  end if;
  select * into v_solicitud from public.solicitudes where id = p_solicitud;
  if v_solicitud.estado <> 'Pendiente' then
    raise exception 'PETMATCH: Esta solicitud ya fue respondida.';
  end if;
  if not p_aceptar then
    update public.solicitudes set estado = 'Rechazada', actualizado_en = now() where id = p_solicitud;
    return;
  end if;
  if not v_mascota.disponible or v_mascota.vence_en <= now() then
    raise exception 'PETMATCH: La publicación no está activa. Renuévala antes de aceptar.';
  end if;
  update public.solicitudes set estado = 'Aceptada', actualizado_en = now() where id = p_solicitud;
  update public.solicitudes set estado = 'Rechazada', actualizado_en = now()
  where mascota_id = v_mascota.id and id <> p_solicitud and estado = 'Pendiente';
  update public.mascotas set disponible = false where id = v_mascota.id;
end;
$$;
revoke all on function public.responder_solicitud(uuid, boolean) from public;
grant execute on function public.responder_solicitud(uuid, boolean) to authenticated;

create or replace function public.marcar_notificaciones_leidas()
returns void language sql security definer set search_path = '' as $$
  update public.notificaciones set leida = true where destinatario_id = auth.uid() and not leida;
$$;
revoke all on function public.marcar_notificaciones_leidas() from public;
grant execute on function public.marcar_notificaciones_leidas() to authenticated;

-- Los avisos se crean en el servidor; el cliente no puede escribir notificaciones.
create or replace function public.avisar_solicitud()
returns trigger language plpgsql security definer set search_path = '' as $$
begin
  if tg_op = 'INSERT' then
    insert into public.notificaciones(destinatario_id, mascota_id, mensaje)
    select m.propietario_id, m.id, 'Recibiste una nueva solicitud de adopción para ' || public.nombre_mascota(m.id) || '.'
    from public.mascotas m where m.id = new.mascota_id;
  elsif new.estado <> old.estado then
    insert into public.notificaciones(destinatario_id, mascota_id, mensaje)
    values (new.solicitante_id, new.mascota_id, 'Tu solicitud para ' || public.nombre_mascota(new.mascota_id)
      || case new.estado when 'Aceptada' then ' fue aceptada. El responsable se pondrá en contacto contigo.'
         else ' fue rechazada.' end);
  end if;
  return new;
end;
$$;
drop trigger if exists solicitudes_avisos on public.solicitudes;
create trigger solicitudes_avisos after insert or update of estado on public.solicitudes
for each row execute function public.avisar_solicitud();

-- Si el responsable borra la publicación, avisa a quienes seguían esperando respuesta.
create or replace function public.avisar_retiro()
returns trigger language plpgsql security definer set search_path = '' as $$
begin
  insert into public.notificaciones(destinatario_id, mensaje)
  select s.solicitante_id, 'La publicación de ' || coalesce(old.nombre, old.especie) || ' fue retirada por su responsable.'
  from public.solicitudes s where s.mascota_id = old.id and s.estado = 'Pendiente';
  return old;
end;
$$;
drop trigger if exists mascotas_retiro on public.mascotas;
create trigger mascotas_retiro before delete on public.mascotas
for each row execute function public.avisar_retiro();

-- Retira del catálogo las publicaciones con más de 10 días y avisa al responsable.
create or replace function public.vencer_publicaciones()
returns integer language plpgsql security definer set search_path = '' as $$
declare v_total integer;
begin
  with vencidas as (
    update public.mascotas set disponible = false, vencida = true
    where disponible and vence_en <= now() returning id, propietario_id
  )
  insert into public.notificaciones(destinatario_id, mascota_id, mensaje)
  select propietario_id, id, 'La publicación de ' || public.nombre_mascota(id)
    || ' venció el plazo de 10 días y se retiró del catálogo. Puedes renovarla desde Mis publicaciones.'
  from vencidas;
  get diagnostics v_total = row_count;
  return v_total;
end;
$$;
revoke all on function public.vencer_publicaciones() from public, anon, authenticated;
grant execute on function public.vencer_publicaciones() to service_role;

-- Tarea programada cada 15 minutos. Funciona aunque la app esté cerrada.
do $$
begin
  if exists(select 1 from pg_available_extensions where name = 'pg_cron') then
    create extension if not exists pg_cron;
    perform cron.schedule('petmatch-vencimiento', '*/15 * * * *', 'select public.vencer_publicaciones()');
  end if;
end $$;

notify pgrst, 'reload schema';
commit;
