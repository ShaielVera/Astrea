# Astrea — Guía de desarrollo

**Requisitos:** .NET SDK 10 (LTS) · Node.js 24 · Docker Desktop.

```
Astrea/
├── backend/            # Solución .NET 10 (Arquitectura Hexagonal)
│   ├── src/            # Domain · Application · Infrastructure · Api
│   └── tests/          # Unitarios · Integración (Testcontainers) · Arquitectura
├── frontend/           # Next.js + React + Tailwind (E2E con Playwright en e2e/)
├── docs/               # SDD, Arquitectura, Roadmap y consigna original
├── docker-compose.yml  # PostgreSQL 17 + pgvector (puerto 5433)
└── .github/workflows/  # CI
```

```bash
# 1. Base de datos (PostgreSQL + pgvector)
docker compose up -d

# 2. Backend → http://localhost:5080
cd backend
dotnet tool restore
dotnet ef database update --project src/Astrea.Infrastructure --startup-project src/Astrea.Api
dotnet run --project src/Astrea.Api --launch-profile http
dotnet test                       # unitarios, integración y arquitectura

# 3. Frontend → http://localhost:3000
cd frontend
npm install
cp .env.example .env.local
npm run dev
npm run build && npm run test:e2e # E2E con Playwright
```

Health checks de la API: `GET /health/live` (la API está levantada) y `GET /health/ready` (además conecta con PostgreSQL).

### 🔐 Autenticación (Firebase)

- El frontend inicia sesión con Firebase Auth (email/contraseña o Google) y envía el **ID Token** como `Authorization: Bearer`.
- La API valida firma, emisor, audiencia (`Firebase:ProjectId`) y vencimiento. Los **roles salen de la base de datos**, nunca del token ni del cliente.
- Al iniciar sesión, el frontend llama a `POST /api/v1/auth/sesion`: la primera vez da de alta al usuario como **Estudiante**.
- Los primeros **Administradores** se definen por email en `Identidad:AdministradoresIniciales` y reciben el rol solo si Firebase verificó su email (cuentas de Google). Para no publicar emails en el repositorio, se cargan como *user secret*:

```bash
cd backend
dotnet user-secrets set "Identidad:AdministradoresIniciales:0" "tu-email@gmail.com" --project src/Astrea.Api
```

- La configuración web de Firebase va en `frontend/.env.local` (ver `frontend/.env.example`).

### 🧪 Tests E2E

`npm run test:e2e` (en `frontend/`) levanta todo el sistema: PostgreSQL y el **Firebase Auth Emulator** en Docker (no hace falta instalar Java), la API en el ambiente `E2E` (base `astrea_e2e`, recreada en cada corrida) y el frontend apuntando al emulador. Los usuarios del emulador se pueden ver en http://localhost:4000.
