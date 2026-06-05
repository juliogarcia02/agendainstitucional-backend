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

En producción el backend usa la cadena `DefaultConnection` definida por el entorno/host y el frontend debe apuntar a la URL pública de la API mediante `API_URL_INTERNAL` o `NEXT_PUBLIC_API_URL`.
