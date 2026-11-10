#!/usr/bin/env bash
# Instalação inicial da API direto na VM (systemd). Requer o .NET (SDK 9 ou superior) e o MySQL já no ar.
# Uso (na raiz do repositório):  sudo deploy/install.sh
set -euo pipefail

SERVICE="dengue-alert-api"
APP_USER="dengue-alert"
ENV_FILE="/etc/dengue-alert-api.env"
UNIT_FILE="/etc/systemd/system/${SERVICE}.service"

[ "$(id -u)" -eq 0 ] || { echo "Execute como root (sudo deploy/install.sh)." >&2; exit 1; }
DOTNET="$(command -v dotnet || true)"
[ -n "$DOTNET" ] || {
  echo "dotnet não encontrado. Instale o SDK 9 (ex.: dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet" >&2
  echo "e ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet) e rode de novo." >&2
  exit 1
}

cd "$(dirname "$0")/.."

id "$APP_USER" >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin "$APP_USER"

env_created=0
if [ ! -f "$ENV_FILE" ]; then
  install -m 600 -o root -g root deploy/dengue-alert-api.env.example "$ENV_FILE"
  env_created=1
fi

sed "s#__DOTNET__#${DOTNET}#" deploy/dengue-alert-api.service > "$UNIT_FILE"
chmod 644 "$UNIT_FILE"
systemctl daemon-reload
systemctl enable "$SERVICE" >/dev/null

if [ "$env_created" -eq 1 ]; then
  # Publica, mas só inicia depois de você preencher as senhas.
  systemctl disable "$SERVICE" >/dev/null 2>&1 || true
  APP_USER="$APP_USER" deploy/update.sh
  systemctl stop "$SERVICE" 2>/dev/null || true
  systemctl enable "$SERVICE" >/dev/null
  echo
  echo "Arquivo de configuração criado: $ENV_FILE"
  echo "1) Edite-o (senha do MySQL, CORS e Admin__ApiKey):  sudo nano $ENV_FILE"
  echo "2) Inicie o serviço:                               sudo systemctl start $SERVICE"
  echo "3) Acompanhe:                                       journalctl -u $SERVICE -f"
else
  deploy/update.sh
fi
