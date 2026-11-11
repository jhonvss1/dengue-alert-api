#!/usr/bin/env bash
# Compila e publica a versão atual do código em /opt/dengue-alert-api e reinicia o serviço.
# Uso (na raiz do repositório, após git pull):  sudo deploy/update.sh
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/dengue-alert-api}"
SERVICE="${SERVICE:-dengue-alert-api}"
APP_USER="${APP_USER:-dengue-alert}"

[ "$(id -u)" -eq 0 ] || { echo "Execute como root (sudo deploy/update.sh)." >&2; exit 1; }
command -v dotnet >/dev/null || { echo "dotnet não encontrado no PATH." >&2; exit 1; }

cd "$(dirname "$0")/.."

BUILD_DIR="$(mktemp -d)"
trap 'rm -rf "$BUILD_DIR"' EXIT

echo "==> Publicando..."
dotnet publish ApiAlertaDengue.csproj -c Release -o "$BUILD_DIR" /p:UseAppHost=false --nologo

managed=0
if systemctl cat "$SERVICE" >/dev/null 2>&1; then managed=1; fi

[ "$managed" -eq 1 ] && systemctl stop "$SERVICE"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR"
cp -a "$BUILD_DIR"/. "$APP_DIR"/
id "$APP_USER" >/dev/null 2>&1 && chown -R "$APP_USER":"$APP_USER" "$APP_DIR"

if [ "$managed" -eq 1 ]; then
  systemctl start "$SERVICE"
  echo "==> Serviço reiniciado. Logs: journalctl -u $SERVICE -f"
else
  echo "==> Publicado em $APP_DIR (serviço $SERVICE ainda não instalado)."
fi
