-- PETMATCH / Dylan Jaramillo. Ejecutar después de 01 y 02 en el mismo proyecto.
-- Publicación de mascotas: datos, fotos (1 a 5), límite de 5 activas, edición, renovación y borrado.
begin;

alter table public.mascotas
  add column if not exists tamano text,
  add column if not exists color text,
  add column if not exists salud text,
  add column if not exists vacunas text check (length(vacunas) <= 200),
  add column if not exists vencida boolean not null default false;

create table if not exists public.mascota_fotos (
  id uuid primary key default gen_random_uuid(),
  mascota_id uuid not null references public.mascotas(id) on delete cascade,
  ruta text not null unique,
  orden smallint not null check (orden between 1 and 5),
  unique (mascota_id, orden)
);
create index if not exists mascota_fotos_mascota_idx on public.mascota_fotos (mascota_id);

alter table public.mascota_fotos enable row level security;
revoke all on public.mascota_fotos from anon, authenticated;
grant select on public.mascota_fotos to anon, authenticated;
grant all on public.mascota_fotos to service_role;

grant select (tamano, color, salud, vacunas) on public.mascotas to anon, authenticated;

-- Ayudantes sin datos personales. El cliente no puede leer propietario_id ni vence_en directamente.
create or replace function public.mascota_visible(p_mascota uuid)
returns boolean language sql stable security definer set search_path = '' as $$
  select exists(select 1 from public.mascotas m where m.id = p_mascota
    and m.disponible and m.vence_en > now() and public.cuenta_disponible(m.propietario_id));
$$;
revoke all on function public.mascota_visible(uuid) from public;
grant execute on function public.mascota_visible(uuid) to anon, authenticated, service_role;

create or replace function public.es_responsable(p_mascota uuid)
returns boolean language sql stable security definer set search_path = '' as $$
  select exists(select 1 from public.mascotas m where m.id = p_mascota and m.propietario_id = auth.uid());
$$;
revoke all on function public.es_responsable(uuid) from public;
grant execute on function public.es_responsable(uuid) to anon, authenticated, service_role;

-- Las fotos de una mascota se ven mientras la publicación se ve en el catálogo.
drop policy if exists mascota_fotos_lectura on public.mascota_fotos;
create policy mascota_fotos_lectura on public.mascota_fotos for select to anon, authenticated
using (public.mascota_visible(mascota_id) or public.es_responsable(mascota_id));

-- Las fotos de mascotas no son datos personales: bucket público para mostrarlas en el catálogo.
insert into storage.buckets(id, name, public, file_size_limit, allowed_mime_types)
values ('pet-photos', 'pet-photos', true, 5242880, array['image/jpeg','image/png'])
on conflict(id) do update set public = true, file_size_limit = 5242880,
  allowed_mime_types = array['image/jpeg','image/png'];

-- La lectura pública usa la URL del bucket; esta política solo permite al dueño administrar sus archivos.
drop policy if exists pet_photos_leer on storage.objects;
create policy pet_photos_leer on storage.objects for select to authenticated using (
  bucket_id = 'pet-photos' and (storage.foldername(name))[1] = (select auth.uid())::text
);
drop policy if exists pet_photos_crear on storage.objects;
create policy pet_photos_crear on storage.objects for insert to authenticated with check (
  bucket_id = 'pet-photos' and (storage.foldername(name))[1] = (select auth.uid())::text
  and public.cuenta_disponible((select auth.uid()))
);
drop policy if exists pet_photos_eliminar on storage.objects;
create policy pet_photos_eliminar on storage.objects for delete to authenticated using (
  bucket_id = 'pet-photos' and (storage.foldername(name))[1] = (select auth.uid())::text
  and not exists(select 1 from public.mascota_fotos f where f.ruta = name)
);

-- Validación común de publicar y editar.
create or replace function public.validar_mascota(
  p_nombre text, p_especie text, p_raza text, p_edad text, p_sexo text, p_tamano text,
  p_color text, p_salud text, p_vacunas text, p_ubicacion text, p_descripcion text)
returns void language plpgsql immutable set search_path = '' as $$
begin
  if coalesce(char_length(btrim(p_nombre)), 0) > 60 then
    raise exception 'PETMATCH: El nombre admite máximo 60 caracteres.';
  end if;
  if p_especie is null or btrim(p_especie) not in ('Perro', 'Gato', 'Otro') then
    raise exception 'PETMATCH: Elige la especie.';
  end if;
  if p_sexo is null or btrim(p_sexo) not in ('Macho', 'Hembra') then
    raise exception 'PETMATCH: Elige el sexo.';
  end if;
  if p_tamano is null or btrim(p_tamano) not in ('Pequeño', 'Mediano', 'Grande') then
    raise exception 'PETMATCH: Elige el tamaño.';
  end if;
  if p_raza is null or char_length(btrim(p_raza)) not between 2 and 60
     or p_edad is null or char_length(btrim(p_edad)) not between 1 and 30 then
    raise exception 'PETMATCH: Completa la raza y la edad.';
  end if;
  if p_salud is null or char_length(btrim(p_salud)) not between 2 and 200 then
    raise exception 'PETMATCH: Describe el estado de salud (de 2 a 200 caracteres).';
  end if;
  if p_ubicacion is null or char_length(btrim(p_ubicacion)) not between 3 and 120 then
    raise exception 'PETMATCH: Escribe la ubicación (de 3 a 120 caracteres).';
  end if;
  if coalesce(char_length(btrim(p_color)), 0) > 60 or coalesce(char_length(btrim(p_vacunas)), 0) > 200
     or coalesce(char_length(btrim(p_descripcion)), 0) > 500 then
    raise exception 'PETMATCH: Color hasta 60, vacunas hasta 200 y comentario hasta 500 caracteres.';
  end if;
end;
$$;
revoke all on function public.validar_mascota(text,text,text,text,text,text,text,text,text,text,text) from public;

-- Publica de forma atómica: datos + fotos ya subidas a la carpeta del usuario.
-- p_portada_url es la URL pública de la primera foto; se comprueba que corresponda a esa ruta.
create or replace function public.publicar_mascota(
  p_nombre text, p_especie text, p_raza text, p_edad text, p_sexo text, p_tamano text,
  p_color text, p_salud text, p_vacunas text, p_ubicacion text, p_descripcion text,
  p_fotos text[], p_portada_url text)
returns uuid language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid(); v_mascota uuid; v_total int := coalesce(array_length(p_fotos, 1), 0);
begin
  if v_id is null or not public.cuenta_disponible(v_id) then
    raise exception 'PETMATCH: Inicia sesión con una cuenta activa.';
  end if;
  if not exists(select 1 from public.perfiles where id = v_id) then
    raise exception 'PETMATCH: Completa tu perfil antes de publicar.';
  end if;
  -- Mismo bloqueo que el perfil: serializa publicaciones simultáneas y la eliminación de cuenta.
  perform pg_advisory_xact_lock(hashtextextended(v_id::text, 0));
  if not public.cuenta_disponible(v_id) then raise exception 'PETMATCH: La cuenta se está eliminando.'; end if;
  perform public.validar_mascota(p_nombre, p_especie, p_raza, p_edad, p_sexo, p_tamano,
    p_color, p_salud, p_vacunas, p_ubicacion, p_descripcion);
  if v_total not between 1 and 5 or (select count(distinct x) from unnest(p_fotos) x) <> v_total then
    raise exception 'PETMATCH: Agrega entre 1 y 5 fotos distintas.';
  end if;
  if exists(select 1 from unnest(p_fotos) x where split_part(x, '/', 1) <> v_id::text
     or not exists(select 1 from storage.objects o where o.bucket_id = 'pet-photos' and o.name = x)) then
    raise exception 'PETMATCH: Sube las fotos antes de publicar.';
  end if;
  if p_portada_url is null or p_portada_url not like 'https://%'
     or right(p_portada_url, char_length('/storage/v1/object/public/pet-photos/' || p_fotos[1]))
        <> '/storage/v1/object/public/pet-photos/' || p_fotos[1] then
    raise exception 'PETMATCH: La foto principal no es válida.';
  end if;
  if (select count(*) from public.mascotas
      where propietario_id = v_id and disponible and vence_en > now()) >= 5 then
    raise exception 'PETMATCH: Ya tienes 5 publicaciones activas. Elimina o espera que una termine.';
  end if;
  insert into public.mascotas(propietario_id, nombre, especie, raza, edad, sexo, tamano, color,
    salud, vacunas, ubicacion, descripcion, foto_url)
  values (v_id, nullif(btrim(p_nombre), ''), btrim(p_especie), btrim(p_raza), btrim(p_edad), btrim(p_sexo),
    btrim(p_tamano), nullif(btrim(p_color), ''), btrim(p_salud), nullif(btrim(p_vacunas), ''),
    btrim(p_ubicacion), nullif(btrim(p_descripcion), ''), p_portada_url)
  returning id into v_mascota;
  insert into public.mascota_fotos(mascota_id, ruta, orden)
  select v_mascota, x, n from unnest(p_fotos) with ordinality as t(x, n);
  return v_mascota;
end;
$$;
revoke all on function public.publicar_mascota(text,text,text,text,text,text,text,text,text,text,text,text[],text) from public;
grant execute on function public.publicar_mascota(text,text,text,text,text,text,text,text,text,text,text,text[],text) to authenticated;

create or replace function public.mis_mascotas()
returns table(id uuid, nombre text, especie text, raza text, edad text, sexo text, tamano text,
  color text, salud text, vacunas text, ubicacion text, descripcion text, foto_url text,
  disponible boolean, vencida boolean, creado_en timestamptz, vence_en timestamptz)
language sql stable security definer set search_path = '' as $$
  select m.id, m.nombre, m.especie, m.raza, m.edad, m.sexo, m.tamano, m.color, m.salud, m.vacunas,
    m.ubicacion, m.descripcion, m.foto_url, m.disponible, m.vencida, m.creado_en, m.vence_en
  from public.mascotas m
  where m.propietario_id = auth.uid() and public.cuenta_disponible(auth.uid())
  order by m.creado_en desc;
$$;
revoke all on function public.mis_mascotas() from public;
grant execute on function public.mis_mascotas() to authenticated;

-- Edita solo los datos; las fotos se definen al publicar.
create or replace function public.editar_mascota(p_mascota uuid,
  p_nombre text, p_especie text, p_raza text, p_edad text, p_sexo text, p_tamano text,
  p_color text, p_salud text, p_vacunas text, p_ubicacion text, p_descripcion text)
returns void language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid();
begin
  if v_id is null or not public.cuenta_disponible(v_id) then
    raise exception 'PETMATCH: Inicia sesión con una cuenta activa.';
  end if;
  perform public.validar_mascota(p_nombre, p_especie, p_raza, p_edad, p_sexo, p_tamano,
    p_color, p_salud, p_vacunas, p_ubicacion, p_descripcion);
  update public.mascotas set nombre = nullif(btrim(p_nombre), ''), especie = btrim(p_especie),
    raza = btrim(p_raza), edad = btrim(p_edad), sexo = btrim(p_sexo), tamano = btrim(p_tamano),
    color = nullif(btrim(p_color), ''), salud = btrim(p_salud), vacunas = nullif(btrim(p_vacunas), ''),
    ubicacion = btrim(p_ubicacion), descripcion = nullif(btrim(p_descripcion), '')
  where id = p_mascota and propietario_id = v_id;
  if not found then raise exception 'PETMATCH: La publicación no existe o no te pertenece.'; end if;
end;
$$;
revoke all on function public.editar_mascota(uuid,text,text,text,text,text,text,text,text,text,text,text) from public;
grant execute on function public.editar_mascota(uuid,text,text,text,text,text,text,text,text,text,text,text) to authenticated;

-- Vuelve a publicar por 10 días una mascota vencida que sigue disponible.
create or replace function public.renovar_mascota(p_mascota uuid)
returns void language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid();
begin
  if v_id is null or not public.cuenta_disponible(v_id) then
    raise exception 'PETMATCH: Inicia sesión con una cuenta activa.';
  end if;
  perform pg_advisory_xact_lock(hashtextextended(v_id::text, 0));
  if not exists(select 1 from public.mascotas where id = p_mascota and propietario_id = v_id
      and (vencida or (disponible and vence_en <= now()))) then
    raise exception 'PETMATCH: Solo puedes renovar tus publicaciones vencidas.';
  end if;
  if (select count(*) from public.mascotas
      where propietario_id = v_id and disponible and vence_en > now()) >= 5 then
    raise exception 'PETMATCH: Ya tienes 5 publicaciones activas.';
  end if;
  update public.mascotas set disponible = true, vencida = false, vence_en = now() + interval '10 days'
  where id = p_mascota;
end;
$$;
revoke all on function public.renovar_mascota(uuid) from public;
grant execute on function public.renovar_mascota(uuid) to authenticated;

-- Borra la publicación y devuelve las rutas de sus fotos para eliminarlas de Storage.
create or replace function public.eliminar_mascota(p_mascota uuid)
returns text[] language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid(); v_rutas text[];
begin
  if v_id is null then raise exception 'PETMATCH: Inicia sesión para continuar.'; end if;
  if not exists(select 1 from public.mascotas where id = p_mascota and propietario_id = v_id) then
    raise exception 'PETMATCH: La publicación no existe o no te pertenece.';
  end if;
  select array_agg(ruta) into v_rutas from public.mascota_fotos where mascota_id = p_mascota;
  delete from public.mascotas where id = p_mascota;
  return coalesce(v_rutas, '{}');
end;
$$;
revoke all on function public.eliminar_mascota(uuid) from public;
grant execute on function public.eliminar_mascota(uuid) to authenticated;

notify pgrst, 'reload schema';
commit;
