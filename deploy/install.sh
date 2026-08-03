#!/usr/bin/env bash
#
# Bojan Store — provisioner.
#
# Reached from the bootstrap at the repository root, or run directly on a
# machine that already has the source:
#
#   sudo bash deploy/install.sh              install, or restart what is there
#   sudo bash deploy/install.sh --rebuild    rebuild images after new code
#   sudo bash deploy/install.sh --defaults   take every default, ask nothing
#
# Installs Docker if the host has none, asks where the site will live, writes a
# .env full of generated secrets, builds the four services and waits for them to
# report healthy. Safe to run twice: an existing .env is never overwritten and
# the database seeder skips every table that already has rows.

set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

readonly ENV_FILE="$ROOT/.env"
readonly ENV_EXAMPLE="$ROOT/.env.example"
readonly CLI_SOURCE="$ROOT/deploy/bojan"
readonly CLI_TARGET="/usr/local/bin/bojan"

if [[ -t 1 ]]; then
  readonly BOLD=$'\033[1m' DIM=$'\033[2m' RED=$'\033[31m' GREEN=$'\033[32m' YELLOW=$'\033[33m' RESET=$'\033[0m'
else
  readonly BOLD='' DIM='' RED='' GREEN='' YELLOW='' RESET=''
fi

step() { printf '\n%s==>%s %s\n' "$BOLD" "$RESET" "$1"; }
info() { printf '    %s\n' "$1"; }
warn() { printf '    %s%s%s\n' "$YELLOW" "$1" "$RESET"; }
ok()   { printf '    %s%s%s\n' "$GREEN" "$1" "$RESET"; }
die()  { printf '\n%serror:%s %s\n\n' "$RED" "$RESET" "$1" >&2; exit 1; }

REBUILD=''
DEFAULTS=''
case "${1:-}" in
  --rebuild)  REBUILD=1 ;;
  --defaults) DEFAULTS=1 ;;
  --help|-h)  awk 'NR>2 { if ($0 !~ /^#/) exit; sub(/^# ?/, ""); print }' "${BASH_SOURCE[0]}"; exit 0 ;;
  '')         ;;
  *)          die "Unknown option: $1 (try --help)" ;;
esac

[[ "$(uname -s)" == "Linux" ]] || die "This installer targets Linux."
[[ "$(id -u)" -eq 0 ]] || die "Run as root (prefix with sudo)."

# --- docker -----------------------------------------------------------------

install_docker() {
  step "Docker"

  if command -v docker >/dev/null 2>&1; then
    ok "Present: $(docker --version)"
  else
    info "Not installed — fetching from get.docker.com."
    curl -fsSL https://get.docker.com -o /tmp/get-docker.sh
    sh /tmp/get-docker.sh >/dev/null
    rm -f /tmp/get-docker.sh
    ok "Installed."
  fi

  docker compose version >/dev/null 2>&1 ||
    die "The 'docker compose' plugin is missing. Install docker-compose-plugin and re-run."

  if docker info >/dev/null 2>&1; then
    ok "Daemon running."
  elif command -v systemctl >/dev/null 2>&1; then
    # `enable` as well as `start`: every service in the compose file restarts
    # unless-stopped, so with the daemon enabled a reboot brings the whole
    # stack back without anyone logging in.
    systemctl enable --now docker
    ok "Daemon started, and enabled at boot."
  else
    die "Docker is installed but not running, and this host has no systemd to start it."
  fi

  docker info >/dev/null 2>&1 || die "The Docker daemon is not reachable."
}

# --- configuration ----------------------------------------------------------

secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 32
  else
    head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n'
  fi
}

# Asks a question, offering a default. `--defaults` turns every prompt into its
# default so the same script can run unattended.
ask() {
  local prompt="$1" default="$2" answer
  if [[ -n "$DEFAULTS" ]]; then
    printf '%s' "$default"
    return
  fi
  read -r -p "    ${prompt} ${DIM}[${default}]${RESET}: " answer </dev/tty || answer=''
  printf '%s' "${answer:-$default}"
}

# Rewrites `KEY=...` in place. `|` as the sed delimiter because values here are
# URLs, and a `/` inside one would end the expression.
set_key() {
  local key="$1" value="$2"
  if grep -qE "^${key}=" "$ENV_FILE"; then
    sed -i "s|^${key}=.*|${key}=${value}|" "$ENV_FILE"
  else
    printf '%s=%s\n' "$key" "$value" >> "$ENV_FILE"
  fi
}

value_of() {
  local value
  value="$(grep -E "^$1=" "$ENV_FILE" 2>/dev/null | head -1 | cut -d= -f2- || true)"
  printf '%s' "${value:-${2:-}}"
}

configure() {
  if [[ -f "$ENV_FILE" ]]; then
    step "Configuration"
    ok "Keeping the existing .env."

    # A file written by an older revision can lack keys the compose file now
    # needs. Add only those, so nothing already set is disturbed.
    while IFS= read -r key; do
      grep -qE "^${key}=" "$ENV_FILE" || printf '%s=\n' "$key" >> "$ENV_FILE"
    done < <(grep -oE '^[A-Z_][A-Z0-9_]*=' "$ENV_EXAMPLE" | tr -d '=')
  else
    step "Configuration"
    [[ -f "$ENV_EXAMPLE" ]] || die ".env.example is missing; cannot generate configuration."
    cp "$ENV_EXAMPLE" "$ENV_FILE"

    printf '\n    %sWhere will this be reached?%s\n' "$BOLD" "$RESET"
    printf '    %sThese are compiled into the browser bundle, so changing them later\n' "$DIM"
    printf '    means re-running with --rebuild.%s\n\n' "$RESET"

    local domain
    domain="$(ask 'Domain (blank for a plain IP/test box)' '')"

    if [[ -n "$domain" ]]; then
      # Strip a scheme if one was pasted in, then assume TLS — a real domain
      # is going behind a proxy that terminates it.
      domain="${domain#http://}"
      domain="${domain#https://}"
      domain="${domain%%/*}"
      set_key PUBLIC_STOREFRONT_URL "https://${domain}"
      set_key PUBLIC_API_URL "https://${domain}"
      info "Storefront and API will be served from https://${domain}"
      warn "Point that name at this host and terminate TLS in front — see the notes at the end."
    else
      local host
      host="$(ask 'Host or IP browsers will use' "$(hostname -I 2>/dev/null | awk '{print $1}' || echo localhost)")"
      local sf_port api_port
      sf_port="$(ask 'Storefront port' '3000')"
      api_port="$(ask 'API port' '7001')"
      set_key STOREFRONT_PORT "$sf_port"
      set_key API_PORT "$api_port"
      set_key ADMIN_PORT "$(ask 'Admin panel port' '3001')"
      set_key PUBLIC_STOREFRONT_URL "http://${host}:${sf_port}"
      set_key PUBLIC_API_URL "http://${host}:${api_port}"
    fi

    ok "Wrote .env."
  fi

  # Every secret still blank gets its own value. Never reuse one across two
  # keys — two secrets that are equal are one secret.
  local generated=0
  for key in POSTGRES_PASSWORD JWT_SIGNING_KEY API_KEY STOREFRONT_AUTH_SECRET ADMIN_AUTH_SECRET; do
    if grep -qE "^${key}=$" "$ENV_FILE"; then
      set_key "$key" "$(secret)"
      generated=1
    fi
  done
  (( generated )) && ok "Generated the missing secrets."

  # Shown once at the end: the seeder stores only a hash, so a forgotten
  # password means recreating the account rather than recovering it.
  if grep -qE '^ADMIN_PASSWORD=$' "$ENV_FILE"; then
    ADMIN_PASSWORD_GENERATED="$(secret | cut -c1-24)"
    set_key ADMIN_PASSWORD "$ADMIN_PASSWORD_GENERATED"
  fi

  chmod 600 "$ENV_FILE"
}

# --- stack ------------------------------------------------------------------

compose() { docker compose --env-file "$ENV_FILE" "$@"; }

bring_up() {
  step "Building images"
  info "First run compiles the .NET API and both Next.js apps. Several minutes."
  compose build ${REBUILD:+--no-cache}

  step "Starting services"
  compose up -d --remove-orphans

  step "Waiting for health"
  # Poll the health state compose already tracks rather than sleeping and
  # hoping: the API migrates and seeds on first boot and is not ready until it
  # says so itself.
  local deadline=$(( SECONDS + 420 )) pending=''
  while (( SECONDS < deadline )); do
    pending="$(compose ps --format '{{.Service}} {{.Health}}' 2>/dev/null |
      awk '$2 != "healthy" && $2 != "" { print $1 }' || true)"
    if [[ -z "$pending" ]]; then
      ok "All services healthy."
      return 0
    fi
    sleep 5
  done

  warn "Still not healthy: $(echo "$pending" | tr '\n' ' ')"
  warn "The stack is up but something is wrong. Inspect it with:  bojan logs"
  return 1
}

install_cli() {
  step "Management tool"
  if [[ -f "$CLI_SOURCE" ]]; then
    install -m 755 "$CLI_SOURCE" "$CLI_TARGET"
    # So the CLI works from any directory, not just this one.
    sed -i "s|^BOJAN_ROOT=.*|BOJAN_ROOT=\"\${BOJAN_ROOT:-$ROOT}\"|" "$CLI_TARGET"
    ok "Installed 'bojan' — run it for status, logs, updates and credentials."
  else
    warn "deploy/bojan not found; skipping the management tool."
  fi
}

summary() {
  printf '\n%s%s%s\n\n' "$BOLD" "Bojan Store is running." "$RESET"
  printf '  storefront   %s\n' "$(value_of PUBLIC_STOREFRONT_URL 'http://localhost:3000')"
  printf '  admin panel  http://localhost:%s\n' "$(value_of ADMIN_PORT 3001)"
  # Fixed by SeedAdminAsync rather than configurable, so it is stated here.
  printf '  operator     admin@bojan.com\n'

  if [[ -n "${ADMIN_PASSWORD_GENERATED:-}" ]]; then
    printf '\n  %sOperator password — shown once, store it now:%s\n' "$YELLOW" "$RESET"
    printf '    %s%s%s\n' "$BOLD" "$ADMIN_PASSWORD_GENERATED" "$RESET"
    printf '  %sAlso in .env as ADMIN_PASSWORD. Change it with: bojan%s\n' "$DIM" "$RESET"
  fi

  printf '\n  %sPorts are published on 127.0.0.1 only.%s Put a reverse proxy in front to\n' "$BOLD" "$RESET"
  printf '  serve them publicly over TLS — nothing here should face the internet\n'
  printf '  directly, and the API trusts X-Api-Key as proof a request came from\n'
  printf '  one of the two Next servers.\n'
  printf '\n  Manage it all with:  %sbojan%s\n\n' "$BOLD" "$RESET"
}

install_docker
configure
install_cli
bring_up || true
summary
