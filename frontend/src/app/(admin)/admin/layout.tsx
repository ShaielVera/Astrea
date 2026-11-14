import { RequiereRol } from "@/features/auth/RequiereRol";
import { ROLES } from "@/features/auth/tipos";

export default function LayoutAdministracion({ children }: LayoutProps<"/admin">) {
  return <RequiereRol rol={ROLES.administrador}>{children}</RequiereRol>;
}
