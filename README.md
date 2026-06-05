# Agenda Institucional

## Ejecución local

Este proyecto está configurado para usar la base de datos productiva también en desarrollo.

### Variables de entorno

Puedes usar la plantilla:

```bash
cp .env.example .env
```

Variables clave:

- `ASPNETCORE_ENVIRONMENT=Development`
- `DatabaseTarget=Production`

Si necesitas forzar una conexión por variable (sin editar appsettings), usa:

- `ConnectionStrings__DevelopmentProductionConnection`
- `ConnectionStrings__DefaultConnection`

### Arranque rápido

Desde `agendainstitucional-backend`:

```bash
./dev.sh start
```

Eso levanta:

- API en `http://localhost:5163`
- Frontend en `http://localhost:3000`

### Ejecutar manualmente

Backend:

```bash
cd src/AgendaInstitucional.Api
ASPNETCORE_ENVIRONMENT=Development DatabaseTarget=Production dotnet run
```

Frontend:

```bash
cd ../agendainstitucional-frontend
API_URL_INTERNAL=http://localhost:5163 NEXT_PUBLIC_API_URL=http://localhost:5163 npm run dev
```

## Producción

El backend usa la cadena `DefaultConnection` definida por el entorno/host.

### Build del frontend (Docker)

Las variables `NEXT_PUBLIC_*` se bakean en el bundle al compilar.
Deben pasarse como build args al construir la imagen:

```bash
docker build \
  --build-arg NEXT_PUBLIC_API_URL=https://agenda.congresogto.gob.mx/api \
  --build-arg AUTH_URL=https://agenda.congresogto.gob.mx \
  -t agendainstitucional-frontend \
  agendainstitucional-frontend/
```

Variables de runtime del contenedor (no necesitan ser build args):

```bash
docker run \
  -e AUTH_SECRET=<secret-fuerte> \
  -e API_URL_INTERNAL=http://<backend-host>:8080 \
  agendainstitucional-frontend
```
