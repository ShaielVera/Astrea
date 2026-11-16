import { RequiereRol } from "@/features/auth/RequiereRol";
import { ROLES } from "@/features/auth/tipos";

export default function LayoutDocente({ children }: LayoutProps<"/docente">) {
  return <RequiereRol rol={ROLES.docente}>{children}</RequiereRol>;
}
