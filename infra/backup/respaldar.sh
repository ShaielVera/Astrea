#!/usr/bin/env bash
# Copia de seguridad de Astrea: la base de datos (pg_dump en formato custom) y los archivos privados
# (PDFs emitidos, fotos de hallazgos y documentos del asistente).
#
# Uso (desde la raíz del repositorio):  infra/backup/respaldar.sh [carpeta-destino]
# Por defecto guarda en ./backups/astrea-AAAAMMDD-HHMMSS/ (carpeta ignorada por git).
#
# Variables opcionales: ASTREA_DB_CONTENEDOR (astrea-db), ASTREA_DB_NOMBRE (astrea), ASTREA_DB_USUARIO (astrea),
# ASTREA_ARCHIVOS (backend/src/Astrea.Api/App_Data/privado).
#
# El respaldo tiene datos personales: guardalo cifrado y fuera del servidor (por ejemplo, en el almacenamiento
# institucional), nunca en el repositorio.
set -euo pipefail

CONTENEDOR="${ASTREA_DB_CONTENEDOR:-astrea-db}"
BASE="${ASTREA_DB_NOMBRE:-astrea}"
USUARIO="${ASTREA_DB_USUARIO:-astrea}"
ARCHIVOS="${ASTREA_ARCHIVOS:-backend/src/Astrea.Api/App_Data/privado}"
DESTINO="${1:-backups}"

carpeta="$DESTINO/astrea-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$carpeta"

echo "Respaldando la base '$BASE' del contenedor '$CONTENEDOR'..."
docker exec "$CONTENEDOR" pg_dump --username "$USUARIO" --dbname "$BASE" --format=custom --no-owner --no-privileges > "$carpeta/base.dump"

# Un respaldo vacío o que no se puede leer no sirve: se verifica en el momento.
if [ ! -s "$carpeta/base.dump" ]; then
  echo "Error: el respaldo de la base quedó vacío. Revisá que el contenedor esté corriendo y que docker exec muestre salida." >&2
  exit 1
fi
docker exec -i "$CONTENEDOR" pg_restore --list < "$carpeta/base.dump" > /dev/null

if [ -d "$ARCHIVOS" ]; then
  echo "Respaldando los archivos privados de '$ARCHIVOS'..."
  tar -czf - -C "$ARCHIVOS" . > "$carpeta/archivos.tar.gz"
else
  echo "Aviso: no existe '$ARCHIVOS'; se respalda solo la base."
fi

(cd "$carpeta" && sha256sum ./* > SHA256SUMS)
echo "Listo: $carpeta"
ls -l "$carpeta"
