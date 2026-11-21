import { redirect } from "next/navigation";

/** Los docentes y sus materias ahora se manejan en "Usuarios y roles" (dirección vieja, por si alguien la tiene guardada). */
export default function Docentes() {
  redirect("/admin/usuarios");
}
