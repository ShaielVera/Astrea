#!/usr/bin/env bash
# Restaura un respaldo hecho con respaldar.sh. REEMPLAZA los datos actuales de la base y de los archivos privados.
#
# Uso (desde la raíz del repositorio, con la API detenida):
#   infra/backup/restaurar.sh backups/astrea-AAAAMMDD-HHMMSS --confirmo
#
# Para probar un respaldo sin tocar la base de trabajo, restauralo en otra base:
#   ASTREA_DB_NOMBRE=astrea_prueba ASTREA_ARCHIVOS=/tmp/astrea-archivos infra/backup/restaurar.sh <carpeta> --confirmo
#
# Variables opcionales: las mismas que respaldar.sh.
set -euo pipefail

CONTENEDOR="${ASTREA_DB_CONTENEDOR:-astrea-db}"
BASE="${ASTREA_DB_NOMBRE:-astrea}"
USUARIO="${ASTREA_DB_USUARIO:-astrea}"
ARCHIVOS="${ASTREA_ARCHIVOS:-backend/src/Astrea.Api/App_Data/privado}"
carpeta="${1:-}"

if [ -z "$carpeta" ] || [ ! -f "$carpeta/base.dump" ]; then
  echo "Indicá la carpeta del respaldo (la que tiene base.dump)." >&2
  exit 1
fi

if [ "${2:-}" != "--confirmo" ]; then
  echo "Esto reemplaza los datos de la base '$BASE' y de '$ARCHIVOS'. Volvé a ejecutarlo agregando --confirmo." >&2
  exit 1
fi

echo "Verificando la integridad del respaldo..."
(cd "$carpeta" && sha256sum --check --quiet SHA256SUMS)

# Si la base no existe (por ejemplo, en un servidor nuevo), se crea.
existe=$(docker exec "$CONTENEDOR" psql --username "$USUARIO" --dbname postgres --tuples-only --no-align \
  --command "SELECT 1 FROM pg_database WHERE datname = '$BASE'")
if [ "$existe" != "1" ]; then
  docker exec "$CONTENEDOR" createdb --username "$USUARIO" "$BASE"
fi

echo "Restaurando la base '$BASE'..."
docker exec -i "$CONTENEDOR" pg_restore --username "$USUARIO" --dbname "$BASE" --clean --if-exists --no-owner --no-privileges --exit-on-error < "$carpeta/base.dump"

if [ -f "$carpeta/archivos.tar.gz" ]; then
  echo "Restaurando los archivos privados en '$ARCHIVOS'..."
  mkdir -p "$ARCHIVOS"
  tar -xzf - -C "$ARCHIVOS" < "$carpeta/archivos.tar.gz"
fi

echo "Listo. Iniciá la API y revisá /health/ready."
