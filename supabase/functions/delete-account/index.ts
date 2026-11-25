// Edge Function. Las credenciales administrativas permanecen en el servidor.
// Recibe SOLO password. El id de la cuenta siempre se obtiene del JWT validado.
const base = Deno.env.get("SUPABASE_URL")!;
const adminKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const publicKey = Deno.env.get("SUPABASE_ANON_KEY")!;
const headers = (key: string, token = key) => ({
  apikey: key, Authorization: `Bearer ${token}`, "Content-Type": "application/json",
});
const json = (status: number, message: string, error_code?: string) => Response.json({ message, error_code }, { status });

async function admin(path: string, method: string, body?: unknown) {
  const result = await fetch(base + path, {
    method, headers: headers(adminKey), body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!result.ok) throw new Error(`Servidor: ${result.status}`);
  return result;
}

Deno.serve(async (req: Request) => {
  if (req.method !== "POST") return json(405, "Método no permitido.");
  const bearer = req.headers.get("Authorization") ?? "";
  if (!bearer.startsWith("Bearer ")) return json(401, "Inicia sesión.");
  const check = await fetch(base + "/auth/v1/user", { headers: headers(publicKey, bearer.slice(7)) });
  if (!check.ok) return json(401, "Sesión no válida.");
  const user = await check.json();
  let body;
  try { body = await req.json(); } catch { return json(400, "Solicitud no válida."); }
  if (typeof body.password !== "string" || !body.password || body.password.length > 72)
    return json(400, "Escribe tu contraseña actual.");

  // Reautenticación real: no basta con el texto ELIMINAR ni con un id enviado por el móvil.
  const reauth = await fetch(base + "/auth/v1/token?grant_type=password", {
    method: "POST", headers: { apikey: publicKey, "Content-Type": "application/json" },
    body: JSON.stringify({ email: user.email, password: body.password }),
  });
  if (!reauth.ok) return json(403, "Contraseña incorrecta o demasiados intentos.", "invalid_credentials");
  const verified = await reauth.json();
  if (verified.user?.id !== user.id) return json(403, "Cuenta no válida.");

  try {
    await admin("/rest/v1/rpc/iniciar_eliminacion", "POST", { p_usuario: user.id });
    // Una cuenta marcada queda bloqueada para escrituras. Los reintentos son seguros.
    // El equipo debe mantener todas las fotos bajo <usuario_id>/<uuid.ext>.
    // Agregar aquí otros buckets propios si se añaden en el futuro.
    for (const bucket of ["avatars", "pet-photos"]) {
      const exists = await fetch(base + "/storage/v1/bucket/" + bucket, { headers: headers(adminKey) });
      if (exists.status === 404) continue;
      if (!exists.ok) throw new Error("No se pudo consultar el bucket.");
      let cleared = false;
      for (let page = 0; page < 50; page++) {
        const listed = await admin("/storage/v1/object/list/" + bucket, "POST", {
          prefix: user.id + "/", limit: 100, offset: 0, sortBy: { column: "name", order: "asc" },
        });
        const items = await listed.json();
        if (!items.length) { cleared = true; break; }
        if (items.some((x: { id: string | null }) => !x.id))
          throw new Error("Se detectaron subcarpetas. Ajustar limpieza antes de eliminar.");
        await admin("/storage/v1/object/" + bucket, "DELETE", {
          prefixes: items.map((x: { name: string }) => user.id + "/" + x.name),
        });
      }
      if (!cleared) throw new Error("Limpieza pendiente; vuelve a intentar.");
    }
    await admin("/auth/v1/admin/users/" + user.id, "DELETE");
    return json(200, "Cuenta eliminada.");
  } catch {
    // No devolver tokens, contraseñas ni respuestas internas a la app.
    return json(503, "La eliminación quedó pendiente. Vuelve a intentarlo desde tu perfil.", "delete_pending");
  }
});
