import { RequiereRol } from "@/features/auth/RequiereRol";
import { ROLES } from "@/features/auth/tipos";

export default function LayoutEstudiante({ children }: LayoutProps<"/">) {
  return <RequiereRol rol={ROLES.estudiante}>{children}</RequiereRol>;
}
