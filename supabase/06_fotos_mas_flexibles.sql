-- PETMATCH: alinear el esquema ya instalado con la publicación simplificada.
-- Ejecutar una vez después de 01-05 en SQL Editor.
-- Permite perfiles sin avatar y publicaciones con 1 a 5 fotos.
begin;

alter table public.perfiles alter column avatar_path drop not null;

create or replace function public.guardar_perfil(
  p_nombres text, p_apellidos text, p_ciudad text, p_direccion text,
  p_celular text, p_cedula text, p_fecha_nacimiento date, p_avatar_path text)
returns void language plpgsql security definer set search_path = '' as $$
declare v_id uuid := auth.uid(); v_anterior public.perfiles_privados%rowtype;
begin
  if v_id is null or not public.cuenta_disponible(v_id) then
    raise exception 'PETMATCH: Inicia sesión con una cuenta activa.';
  end if;
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

notify pgrst, 'reload schema';
commit;
