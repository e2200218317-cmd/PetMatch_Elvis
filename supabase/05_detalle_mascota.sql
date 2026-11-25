-- Detalle seguro de una mascota para la aplicación móvil.
-- Ejecutar después de 04_solicitudes_notificaciones.sql.
begin;

create or replace function public.detalle_mascota(p_mascota uuid)
returns table(
  id uuid, nombre text, especie text, raza text, edad text, sexo text, tamano text,
  color text, salud text, vacunas text, ubicacion text, descripcion text, foto_url text,
  propietario_id uuid, fotos text[])
language sql stable security definer set search_path = '' as $$
  select m.id, m.nombre, m.especie, m.raza, m.edad, m.sexo, m.tamano,
    m.color, m.salud, m.vacunas, m.ubicacion, m.descripcion, m.foto_url,
    m.propietario_id, coalesce(array_agg(f.ruta order by f.orden)
      filter (where f.ruta is not null), '{}'::text[])
  from public.mascotas m
  left join public.mascota_fotos f on f.mascota_id = m.id
  where m.id = p_mascota
    and (public.mascota_visible(m.id)
      or (auth.uid() is not null and public.es_responsable(m.id)))
  group by m.id;
$$;
revoke all on function public.detalle_mascota(uuid) from public;
grant execute on function public.detalle_mascota(uuid) to anon, authenticated;

create or replace function public.mis_solicitudes()
returns table(id uuid, mascota_id uuid, mascota text, estado text, mensaje text, creado_en timestamptz)
language sql stable security definer set search_path = '' as $$
  select s.id, s.mascota_id, coalesce(m.nombre, m.especie), s.estado, s.mensaje, s.creado_en
  from public.solicitudes s
  join public.mascotas m on m.id = s.mascota_id
  where s.solicitante_id = auth.uid() and public.cuenta_disponible(auth.uid())
  order by s.creado_en desc;
$$;
revoke all on function public.mis_solicitudes() from public;
grant execute on function public.mis_solicitudes() to authenticated;

notify pgrst, 'reload schema';
commit;
