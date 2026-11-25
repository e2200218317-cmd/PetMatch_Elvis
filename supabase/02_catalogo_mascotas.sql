-- Ejecutar después de 01_usuarios_perfiles.sql en el mismo proyecto.
begin;

create table if not exists public.mascotas (
  id uuid primary key default gen_random_uuid(),
  propietario_id uuid not null references public.perfiles(id) on delete cascade,
  nombre text,
  especie text not null check (length(btrim(especie)) between 1 and 60),
  raza text,
  edad text,
  sexo text,
  ubicacion text not null check (length(btrim(ubicacion)) between 1 and 120),
  descripcion text check (length(descripcion) <= 500),
  foto_url text check (foto_url like 'https://%'),
  disponible boolean not null default true,
  creado_en timestamptz not null default now(),
  vence_en timestamptz not null default (now() + interval '10 days')
);

create index if not exists mascotas_propietario_idx on public.mascotas (propietario_id);
create index if not exists mascotas_catalogo_idx
  on public.mascotas (creado_en desc, id desc) where disponible;

alter table public.mascotas enable row level security;
revoke all on public.mascotas from anon, authenticated;
grant select (id, nombre, especie, raza, edad, sexo, ubicacion, descripcion,
              foto_url, creado_en) on public.mascotas to anon, authenticated;
grant all on public.mascotas to service_role;

drop policy if exists mascotas_catalogo_lectura on public.mascotas;
create policy mascotas_catalogo_lectura on public.mascotas
  for select to anon, authenticated
  using (disponible and vence_en > now() and public.cuenta_disponible(propietario_id));

notify pgrst, 'reload schema';
commit;
