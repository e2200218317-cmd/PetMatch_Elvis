// npm install --no-save @electric-sql/pglite
// node tests/database.test.mjs
// PostgreSQL embebido; simula solo los esquemas Auth/Storage, no los servicios externos.
import { PGlite } from '@electric-sql/pglite';
import { readFile } from 'node:fs/promises';
const db = new PGlite();
let passed = 0;
function ok(condition, label) { if (!condition) throw new Error(label); passed++; console.log('OK', label); }
async function denied(fn, label) { try { await fn(); } catch { ok(true, label); return; } throw new Error('Permitido por error: ' + label); }
async function as(role, id, fn) {
  await db.exec(`set role ${role}`);
  await db.query("select set_config('request.jwt.claim.sub', $1, false)", [id ?? '']);
  try { return await fn(); } finally { await db.exec('reset role'); }
}
const a = '11111111-1111-1111-1111-111111111111', b = '22222222-2222-2222-2222-222222222222', c = '33333333-3333-3333-3333-333333333333';
await db.exec(`
create role anon; create role authenticated; create role service_role bypassrls;
create schema auth; create schema storage;
grant usage on schema public,auth,storage to anon,authenticated,service_role;
create table auth.users(id uuid primary key, email text);
create function auth.uid() returns uuid language sql stable as $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
create table storage.buckets(id text primary key,name text,public boolean,file_size_limit bigint,allowed_mime_types text[]);
create table storage.objects(id uuid primary key default gen_random_uuid(),bucket_id text,name text);
create function storage.foldername(name text) returns text[] language sql immutable as $$ select string_to_array(name, '/') $$;
alter table storage.objects enable row level security;
grant select,insert,delete,update on storage.objects to anon,authenticated;
insert into auth.users values('${a}','a@example.com'),('${b}','b@example.com'),('${c}','c@example.com');
insert into storage.objects(bucket_id,name) values('avatars','${a}/photo.jpg'),('avatars','${b}/photo.jpg'),('avatars','${c}/photo.jpg');
`);
const sql = await readFile(new URL('../supabase/01_usuarios_perfiles.sql', import.meta.url), 'utf8');
await db.exec(sql); await db.exec(sql); ok(true, 'migración ejecutable y repetible');
const save = (id, cedula, date = '2000-01-01', avatar = id + '/photo.jpg') => db.query(
  "select public.guardar_perfil('Ana','Prueba','El Coca','Calle de pruebas','+593991234567',$1,$2::date,$3)", [cedula,date,avatar]);
await as('authenticated',a,()=>save(a,'0000000001'));
await as('authenticated',b,()=>save(b,'0000000002')); ok(true, 'dos perfiles completos');
await as('anon',null,async()=>{
  ok((await db.query('select * from perfiles')).rows.length === 2,'visitante ve nombres y ciudad');
  await denied(()=>db.query('select * from perfiles_privados'),'visitante no ve cédulas');
  await denied(()=>db.query('select * from contactos_perfil'),'visitante no ve contactos');
  await denied(()=>save(c,'0000000003'),'visitante no puede crear perfil');
  await denied(()=>db.query('select mi_perfil()'),'visitante no llama RPC privada');
});
await as('authenticated',a,async()=>{
  const rows=(await db.query('select * from perfiles_privados')).rows;
  ok(rows.length === 1 && rows[0].usuario_id === a, 'usuario A solo ve su cédula');
  ok((await db.query('select * from mi_perfil()')).rows[0].email === 'a@example.com','perfil propio obtiene correo correcto');
  await denied(()=>db.query("update perfiles set nombres='Atacante' where id=$1",[b]),'no modifica perfil B');
  await denied(()=>save(a,'0000000009'),'cédula inmutable');
  await denied(()=>save(a,'0000000001','2000-01-01',b+'/photo.jpg'),'rechaza avatar de otra persona');
  await denied(()=>db.query('select iniciar_eliminacion($1)',[b]),'RPC administrativa bloqueada al cliente');
  const contact=(await db.query('select * from ver_perfil_contacto($1)',[b])).rows[0];
  ok(contact.celular && !('cedula' in contact) && !('email' in contact),'contacto no expone identidad privada');
  await denied(()=>db.query("insert into storage.objects(bucket_id,name) values('avatars',$1)",[b+'/hacked.jpg']),'no carga fotos en carpeta ajena');
  ok((await db.query("delete from storage.objects where name=$1 returning id",[a+'/photo.jpg'])).rows.length === 0,'no elimina foto actualmente vinculada');
});
await as('authenticated',c,async()=>{
  await denied(()=>save(c,'0000000001'),'cédula duplicada bloqueada');
  ok((await db.query('select * from mi_perfil()')).rows.length===0,'fallo de cédula no deja perfil parcial');
  await denied(()=>save(c,'0000000003','2020-01-01'),'menor rechazado en servidor');
});
await as('service_role',null,()=>db.query('select iniciar_eliminacion($1)',[a]));
await as('authenticated',a,async()=>{
  await denied(()=>save(a,'0000000001'),'cuenta en eliminación no puede guardar');
  await denied(()=>db.query("insert into storage.objects(bucket_id,name) values('avatars',$1)",[a+'/new.jpg']),'cuenta en eliminación no sube fotos');
  ok((await db.query('select * from mi_perfil()')).rows[0].eliminando,'puede ver eliminación pendiente para reintentar');
});
await as('anon',null,async()=>ok((await db.query('select * from perfiles')).rows.length===1,'perfil en eliminación oculto'));
await db.query('delete from auth.users where id=$1',[a]);
ok((await db.query('select * from perfiles_privados where usuario_id=$1',[a])).rows.length===0,'borrado auth elimina perfil y datos privados en cascada');
await db.close(); console.log(`\n${passed} comprobaciones correctas.`);
