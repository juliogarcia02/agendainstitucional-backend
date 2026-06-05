#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$ROOT_DIR/.." && pwd)"
BACKEND_DIR="$ROOT_DIR/src/AgendaInstitucional.Api"
FRONTEND_DIR="$PROJECT_ROOT/agendainstitucional-frontend"
RUN_DIR="$PROJECT_ROOT/.run"
LOG_DIR="$RUN_DIR/logs"
PID_BACKEND="$RUN_DIR/backend.pid"
PID_FRONTEND="$RUN_DIR/frontend.pid"

API_PORT="5163"
WEB_PORT="3000"

mkdir -p "$LOG_DIR"

kill_pid_file() {
  local pid_file="$1"
  if [[ -f "$pid_file" ]]; then
    local pid
    pid="$(cat "$pid_file" || true)"
    if [[ -n "${pid:-}" ]] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
      sleep 1
      if kill -0 "$pid" 2>/dev/null; then
        kill -9 "$pid" 2>/dev/null || true
      fi
    fi
    rm -f "$pid_file"
  fi
}

kill_port() {
  local port="$1"
  local pids
  pids="$(lsof -ti tcp:"$port" 2>/dev/null || true)"
  if [[ -n "${pids:-}" ]]; then
    echo "$pids" | xargs kill -9 2>/dev/null || true
  fi
}

wait_for_port() {
  local port="$1"
  local retries=40
  local i

  for ((i=1; i<=retries; i++)); do
    if lsof -i tcp:"$port" >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.5
  done

  return 1
}

start_backend() {
  echo "Iniciando API en puerto $API_PORT..."
  (
    cd "$BACKEND_DIR"
    ASPNETCORE_ENVIRONMENT=Development DatabaseTarget=Production dotnet run >"$LOG_DIR/backend.log" 2>&1
  ) &
  echo $! >"$PID_BACKEND"

  if wait_for_port "$API_PORT"; then
    echo "API iniciada. Log: $LOG_DIR/backend.log"
  else
    echo "No se pudo iniciar la API. Revisa: $LOG_DIR/backend.log"
    return 1
  fi
}

start_frontend() {
  echo "Iniciando Frontend en puerto $WEB_PORT..."
  (
    cd "$FRONTEND_DIR"
    if [[ ! -d node_modules ]]; then
      npm install
    fi
    AUTH_SECRET="agenda-local-dev-secret-2026" API_URL_INTERNAL="http://localhost:$API_PORT" NEXT_PUBLIC_API_URL="http://localhost:$API_PORT" npm run dev >"$LOG_DIR/frontend.log" 2>&1
  ) &
  echo $! >"$PID_FRONTEND"

  if wait_for_port "$WEB_PORT"; then
    echo "Frontend iniciado. Log: $LOG_DIR/frontend.log"
  else
    echo "No se pudo iniciar el frontend. Revisa: $LOG_DIR/frontend.log"
    return 1
  fi
}

start_all() {
  stop_all

  kill_port "$API_PORT"
  kill_port "$WEB_PORT"

  start_backend
  start_frontend

  echo ""
  echo "Proyecto iniciado correctamente"
  echo "API:      http://localhost:$API_PORT"
  echo "Frontend: http://localhost:$WEB_PORT"
  echo ""
  echo "Para detener todo: ./dev.sh stop"
}

stop_all() {
  kill_pid_file "$PID_BACKEND"
  kill_pid_file "$PID_FRONTEND"
  kill_port "$API_PORT"
  kill_port "$WEB_PORT"
  echo "Procesos detenidos."
}

status_all() {
  echo "Estado de puertos:"
  if lsof -i tcp:"$API_PORT" >/dev/null 2>&1; then
    echo "- API ($API_PORT): activa"
  else
    echo "- API ($API_PORT): inactiva"
  fi

  if lsof -i tcp:"$WEB_PORT" >/dev/null 2>&1; then
    echo "- Frontend ($WEB_PORT): activo"
  else
    echo "- Frontend ($WEB_PORT): inactivo"
  fi
}

cmd="${1:-start}"

case "$cmd" in
  start)
    start_all
    ;;
  stop)
    stop_all
    ;;
  restart)
    stop_all
    start_all
    ;;
  status)
    status_all
    ;;
  *)
    echo "Uso: ./dev.sh [start|stop|restart|status]"
    exit 1
    ;;
esac
