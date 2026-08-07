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
#   sudo bash deploy/install.sh --web-only   redo nginx and the certificate
#
# Installs everything the host is missing — Docker, and, when a domain is given,
# nginx and certbot — asks where the site will live, writes a .env full of
# generated secrets, builds the four services and waits for them to report
# healthy. Safe to run twice: an existing .env is never overwritten and the
# database seeder skips every table that already has rows.
#
# `--web-only` is what `b-ui domain` calls after changing the address: it
# rewrites the vhost and re-issues the certificate without touching the stack.

set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

readonly ENV_FILE="$ROOT/.env"
readonly ENV_EXAMPLE="$ROOT/.env.example"
readonly CLI_SOURCE="$ROOT/deploy/b-ui"
readonly CLI_TARGET="/usr/local/bin/b-ui"
readonly VHOST="/etc/nginx/sites-available/bojan"

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
WEB_ONLY=''
case "${1:-}" in
  --rebuild)  REBUILD=1 ;;
  --defaults) DEFAULTS=1 ;;
  --web-only) WEB_ONLY=1 ;;
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
    # needs. Add only those, so nothing already set is disturbed — and add them
    # with the value the example gives rather than blank.
    #
    # Blank is what this used to write, and it is not the same thing as absent:
    # a key the example ships as `SEED_ENABLED=true` arrived as `SEED_ENABLED=`,
    # which is a set-but-empty variable, which the API reads as false. An
    # upgraded deployment quietly stopped seeding, and would have had no
    # operator account at all had it been a first run.
    local added=0 repaired=0
    while IFS= read -r line; do
      local key="${line%%=*}" value="${line#*=}"

      if ! grep -qE "^${key}=" "$ENV_FILE"; then
        printf '%s\n' "$line" >> "$ENV_FILE"
        added=$(( added + 1 ))
      elif [[ -n "$value" ]] && grep -qE "^${key}=$" "$ENV_FILE"; then
        # Present but empty, where the example ships a value. That is what the
        # earlier version of this loop wrote, and a blank is not an unset: the
        # API reads `SEED_ENABLED=` as false. Repaired rather than left, so a
        # deployment that went through that version is not stuck with settings
        # it never chose. Blanks the example also leaves blank — every secret —
        # are not touched here; they are filled below.
        set_key "$key" "$value"
        repaired=$(( repaired + 1 ))
      fi
    done < <(grep -E '^[A-Z_][A-Z0-9_]*=' "$ENV_EXAMPLE")

    (( added > 0 ))    && info "Added $added new setting(s) from .env.example."
    (( repaired > 0 )) && warn "Restored $repaired setting(s) that had been left blank."
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
      # gets nginx and a certificate below.
      domain="${domain#http://}"
      domain="${domain#https://}"
      domain="${domain%%/*}"

      [[ "$domain" =~ ^[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ ]] ||
        die "\"$domain\" does not look like a domain. Leave it blank for a plain IP box."

      set_key PUBLIC_DOMAIN "$domain"
      set_key PUBLIC_STOREFRONT_URL "https://${domain}"
      set_key PUBLIC_ADMIN_URL "https://admin.${domain}"
      set_key PUBLIC_API_URL "https://${domain}"

      # Let's Encrypt sends expiry warnings here. Optional, but a certificate
      # that lapses silently takes the shop down with it.
      #
      # Checked rather than forwarded. An address typed with the keyboard still
      # in a Persian layout arrives as Persian letters, which looks like an
      # address to nobody and which the ACME server rejects — taking the whole
      # certificate with it. Better to register without an address than to fail
      # over one that was never going to work.
      local contact
      contact="$(ask 'E-mail for certificate expiry notices (optional)' '')"
      if [[ -n "$contact" && ! "$contact" =~ ^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ ]]; then
        warn "\"$contact\" is not an e-mail address — ignoring it."
        info "Add one later with:  b-ui  →  Domain management"
        contact=''
      fi
      set_key TLS_CONTACT_EMAIL "$contact"

      info "Storefront:  https://${domain}"
      info "Admin panel: https://admin.${domain}"
      warn "Both names, and www, must already point at this host — certbot checks."
    else
      local host
      host="$(ask 'Host or IP browsers will use' "$(hostname -I 2>/dev/null | awk '{print $1}' || echo localhost)")"
      local sf_port api_port admin_port
      sf_port="$(ask 'Storefront port' '3000')"
      api_port="$(ask 'API port' '7001')"
      admin_port="$(ask 'Admin panel port' '3001')"
      set_key STOREFRONT_PORT "$sf_port"
      set_key API_PORT "$api_port"
      set_key ADMIN_PORT "$admin_port"
      set_key PUBLIC_STOREFRONT_URL "http://${host}:${sf_port}"
      set_key PUBLIC_ADMIN_URL "http://${host}:${admin_port}"
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

# --- web server ---------------------------------------------------------------

# Everything in front of the containers. Skipped entirely when no domain was
# given: on a plain IP there is nothing to issue a certificate against, and the
# published loopback ports are reachable over an ssh tunnel, which is the right
# way to look at a test box anyway.
configure_web() {
  local domain; domain="$(value_of PUBLIC_DOMAIN)"

  if [[ -z "$domain" ]]; then
    info "No domain set — skipping nginx and TLS."
    return 0
  fi

  step "Web server"

  if ! command -v nginx >/dev/null 2>&1; then
    info "Installing nginx and certbot."
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -qq
    apt-get install -y -qq nginx certbot python3-certbot-nginx >/dev/null
    ok "Installed."
  else
    ok "nginx present."
  fi

  write_vhost "$domain"
  provision_tls "$domain"
  configure_firewall
}

# One vhost, two server names.
#
# The storefront answers on the domain and the panel on `admin.` — a subdomain
# rather than a path because neither Next app sets `basePath`, and an app served
# under a prefix it was not built for returns HTML whose every asset URL is
# wrong.
#
# The API is deliberately *not* proxied. Nothing in either browser bundle calls
# it: every use of NEXT_PUBLIC_API_BASE_URL is inside a server-side route
# handler, so the two Next servers talk to it over the compose network and the
# internet never needs to. The one exception is uploaded product media, which
# the API serves at /media and an <img> does fetch — so that path, and only that
# path, is forwarded.
write_vhost() {
  local domain="$1"
  local storefront_port admin_port api_port
  storefront_port="$(value_of STOREFRONT_PORT 3000)"
  admin_port="$(value_of ADMIN_PORT 3001)"
  api_port="$(value_of API_PORT 7001)"

  info "Writing $VHOST"
  cat > "$VHOST" <<NGINX
# Managed by deploy/install.sh — edits are overwritten on the next run.

# The forwarded headers are scoped to these two servers rather than declared at
# the top of the file: a bare proxy_set_header here would land in nginx's http
# context and apply to every other site this host serves.
#
# X-Forwarded-For is what the API reads to rate-limit on the caller's real
# address; it trusts it because the only peer that can set it is this proxy.

server {
    listen 80;
    listen [::]:80;
    # Not admin.${domain} — naming it here as well would make this the first
    # block matching that name, and nginx would answer the panel's subdomain
    # with the storefront.
    server_name ${domain} www.${domain};

    proxy_set_header Host              \$host;
    proxy_set_header X-Real-IP         \$remote_addr;
    proxy_set_header X-Forwarded-For   \$proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto \$scheme;

    # Uploaded product images, served by the API from the volume it writes
    # them to. Shoppers see these on every catalogue page, so this has to be
    # here and not only on the panel's vhost. It is the one path of the API
    # that is reachable from outside at all.
    location /media/ {
        proxy_pass http://127.0.0.1:${api_port};
    }

    # certbot rewrites this block to redirect to https once it has a
    # certificate; until then the site answers over plain http.
    location / {
        proxy_pass http://127.0.0.1:${storefront_port};
        proxy_http_version 1.1;
        proxy_set_header Upgrade    \$http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}

server {
    listen 80;
    listen [::]:80;
    server_name admin.${domain};

    # The panel is where uploads are made; the default of 1m rejects a photo
    # from any recent phone.
    client_max_body_size 25m;

    proxy_set_header Host              \$host;
    proxy_set_header X-Real-IP         \$remote_addr;
    proxy_set_header X-Forwarded-For   \$proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto \$scheme;

    location /media/ {
        proxy_pass http://127.0.0.1:${api_port};
    }

    location / {
        proxy_pass http://127.0.0.1:${admin_port};
        proxy_http_version 1.1;
        proxy_set_header Upgrade    \$http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
NGINX

  # The default site answers on the same port and would win for any name this
  # file does not list, which is how a fresh box serves "Welcome to nginx" to
  # somebody who has just pointed DNS at it.
  rm -f /etc/nginx/sites-enabled/default
  ln -sf "$VHOST" /etc/nginx/sites-enabled/bojan

  if nginx -t >/dev/null 2>&1; then
    systemctl reload nginx 2>/dev/null || systemctl restart nginx
    ok "nginx serving ${domain} and admin.${domain}"
  else
    nginx -t
    die "nginx rejected the generated configuration — the output above says why."
  fi
}

# Certificates for both names, and the redirect to https.
#
# Non-fatal on purpose: DNS not having propagated yet is the normal reason this
# fails, and it is not a reason to leave the operator with no site at all. The
# stack is already up over http, and `b-ui ssl` retries.
provision_tls() {
  local domain="$1"
  local email; email="$(value_of TLS_CONTACT_EMAIL)"

  step "TLS certificate"

  if certbot certificates 2>/dev/null | grep -q "Domains:.*\b${domain}\b"; then
    ok "A certificate for ${domain} already exists."
    return 0
  fi

  local -a args=(--nginx --non-interactive --agree-tos --redirect
                 -d "$domain" -d "www.${domain}" -d "admin.${domain}")

  # Re-checked here and not only where it was typed: this also runs from
  # `b-ui domain`, and an address that a previous install already wrote into
  # .env would otherwise fail the issuance every time it was retried.
  if [[ -n "$email" && ! "$email" =~ ^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$ ]]; then
    warn "TLS_CONTACT_EMAIL in .env is not an e-mail address — registering without one."
    email=''
  fi

  if [[ -n "$email" ]]; then
    args+=(--email "$email")
  else
    args+=(--register-unsafely-without-email)
  fi

  if certbot "${args[@]}"; then
    ok "Certificate issued; http now redirects to https."
  else
    warn "certbot could not issue a certificate."
    info "The usual cause is DNS: ${domain}, www and admin must all resolve here first."
    info "The site is up over http meanwhile. Retry with:  b-ui ssl"
  fi
}

# Only what has to be reachable. The published container ports are on loopback
# already, so this is about the host's own listeners.
configure_firewall() {
  command -v ufw >/dev/null 2>&1 || return 0

  step "Firewall"

  # Before anything else — a rule set that allows http but not ssh is how a
  # remote install ends with nobody able to log back in.
  ufw allow OpenSSH >/dev/null 2>&1 || ufw allow 22/tcp >/dev/null 2>&1 || true
  ufw allow 'Nginx Full' >/dev/null 2>&1 || { ufw allow 80/tcp; ufw allow 443/tcp; } >/dev/null 2>&1 || true

  if ufw status | head -1 | grep -q inactive; then
    info "ufw is installed but inactive; leaving it that way."
    info "Rules for ssh and nginx are in place if you enable it:  ufw enable"
  else
    ufw reload >/dev/null 2>&1 || true
    ok "ssh and nginx allowed."
  fi
}

# --- stack ------------------------------------------------------------------

compose() { docker compose --env-file "$ENV_FILE" "$@"; }

# The bootstrap at the repository root updates the checkout before handing over,
# but running this script directly — which is what the help text and every
# retry instruction suggest — skips that. Rebuilding then recompiles whatever
# was already on disk, so an operator told "pull the fix and rebuild" does the
# second half and silently repeats the first failure.
update_checkout() {
  [[ -d "$ROOT/.git" ]] || return 0
  command -v git >/dev/null 2>&1 || return 0

  step "Source"
  git config --global --add safe.directory "$ROOT" 2>/dev/null || true

  local before; before="$(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)"

  if git -C "$ROOT" fetch --depth 1 origin main >/dev/null 2>&1 &&
     git -C "$ROOT" reset --hard origin/main >/dev/null 2>&1; then
    local after; after="$(git -C "$ROOT" rev-parse --short HEAD)"
    if [[ "$before" == "$after" ]]; then
      ok "Already at $after."
    else
      ok "Updated $before -> $after."
    fi
  else
    # Not fatal: a box with no route to GitHub should still be able to rebuild
    # what it has.
    warn "Could not reach the repository — building the checkout as it stands ($before)."
  fi
}

bring_up() {
  step "Building images"
  info "First run compiles the .NET API and both Next.js apps. Several minutes."

  # Fatal, and said plainly. This used to run under `bring_up || true`, so a
  # failed build fell through to a health check that then found nothing wrong —
  # because a service whose image never built has no container, and a container
  # that does not exist reports no health at all. The installer congratulated
  # the operator on a stack that was missing half its services.
  if ! compose build ${REBUILD:+--no-cache}; then
    die "One or more images failed to build — the compiler output above says which.
    Nothing was started. Fix the error and run this again."
  fi

  step "Starting services"
  compose up -d --remove-orphans

  step "Waiting for health"

  # What the compose file says should be running. Checking against this rather
  # than against whatever happens to be up is the whole point: absence is the
  # failure mode that looked like success.
  local -a expected
  mapfile -t expected < <(compose config --services)

  local deadline=$(( SECONDS + 420 )) problems='' doomed=''
  while (( SECONDS < deadline )); do
    problems=''
    doomed=''
    local state
    for service in "${expected[@]}"; do
      # `State` is running/exited/restarting/…; `Health` is empty for a service
      # that declares no healthcheck, in which case running is as much as can
      # be asked of it.
      state="$(compose ps --format '{{.Service}} {{.State}} {{.Health}}' 2>/dev/null |
        awk -v s="$service" '$1 == s { print $2 " " $3 }')"

      case "$state" in
        '')                 problems+="$service (no container) "; doomed+="$service " ;;
        running\ healthy)   ;;
        running\ )          ;;
        running\ starting)  problems+="$service (starting) " ;;
        # A container that keeps dying is not going to be healthy in six more
        # minutes — it is failing on boot and will fail again the same way.
        # Waiting out the window only delayed the log that says why.
        restarting*|exited*|dead*)
                            problems+="$service (${state% }) "; doomed+="$service " ;;
        *)                  problems+="$service (${state% }) " ;;
      esac
    done

    if [[ -z "$problems" ]]; then
      ok "All ${#expected[@]} services running and healthy."
      return 0
    fi

    if [[ -n "$doomed" ]]; then
      warn "Not going to recover: $doomed"
      break
    fi

    sleep 5
  done

  warn "Not ready: $problems"

  # Show why, here, rather than leaving the operator to go and ask. A container
  # that exits on boot has already printed the reason and then scrolled it off
  # behind a wall of build output; the whole point of failing is to say what
  # failed. Only the services actually in trouble, and only the tail.
  for service in "${expected[@]}"; do
    [[ "$problems" == *"$service "* ]] || continue
    printf '\n%s--- %s ---%s\n' "$DIM" "$service" "$RESET"
    compose logs --tail=30 --no-log-prefix "$service" 2>&1 | sed 's/^/    /' || true
  done

  printf '\n'
  warn "Full logs:  b-ui logs"
  return 1
}

install_cli() {
  step "Management tool"
  if [[ -f "$CLI_SOURCE" ]]; then
    install -m 755 "$CLI_SOURCE" "$CLI_TARGET"
    # So the CLI works from any directory, not just this one.
    sed -i "s|^BOJAN_ROOT=.*|BOJAN_ROOT=\"\${BOJAN_ROOT:-$ROOT}\"|" "$CLI_TARGET"
    ok "Installed 'b-ui' — run it bare for the menu, or with a subcommand."
  else
    warn "deploy/b-ui not found; skipping the management tool."
  fi
}

summary() {
  local domain; domain="$(value_of PUBLIC_DOMAIN)"

  printf '\n%s%s%s\n\n' "$BOLD" "Bojan Store is running." "$RESET"
  printf '  storefront   %s\n' "$(value_of PUBLIC_STOREFRONT_URL 'http://localhost:3000')"
  printf '  admin panel  %s\n' "$(value_of PUBLIC_ADMIN_URL "http://localhost:$(value_of ADMIN_PORT 3001)")"
  # Fixed by SeedAdminAsync rather than configurable, so it is stated here.
  printf '  operator     admin@bojan.com\n'

  if [[ -n "${ADMIN_PASSWORD_GENERATED:-}" ]]; then
    printf '\n  %sOperator password — shown once, store it now:%s\n' "$YELLOW" "$RESET"
    printf '    %s%s%s\n' "$BOLD" "$ADMIN_PASSWORD_GENERATED" "$RESET"
    printf '  %sAlso in .env as ADMIN_PASSWORD. Change it with: b-ui%s\n' "$DIM" "$RESET"
  fi

  if [[ -n "$domain" ]]; then
    printf '\n  %snginx terminates TLS%s and forwards to the containers, which publish on\n' "$BOLD" "$RESET"
    printf '  127.0.0.1 only. The API is not proxied at all — nothing in either browser\n'
    printf '  bundle calls it, so only /media, where uploaded product images are\n'
    printf '  served, is reachable from outside.\n'
  else
    printf '\n  %sNo domain was given, so nothing is published.%s The ports are on\n' "$BOLD" "$RESET"
    printf '  127.0.0.1 — reach them over an ssh tunnel, or add a domain later with\n'
    printf '  %sb-ui%s, which will install nginx and issue a certificate.\n' "$BOLD" "$RESET"
  fi

  printf '\n  Manage it all with:  %sb-ui%s\n\n' "$BOLD" "$RESET"
}

# `b-ui domain` calls this after rewriting the address: redo the vhost and the
# certificate, leave the containers alone. Everything it needs is already in
# .env, so none of the prompts below run.
if [[ -n "$WEB_ONLY" ]]; then
  [[ -f "$ENV_FILE" ]] || die "No .env at $ENV_FILE — run the installer without --web-only first."
  configure_web
  exit 0
fi

# Before anything is built from it.
update_checkout

install_docker
configure
install_cli
# Before the stack: nginx answering on :80 is what certbot needs to prove the
# domain, and that does not depend on the containers being up yet.
configure_web

# Not `|| true`. A stack that did not come up is still worth summarising — the
# operator needs the addresses and the password either way — but the exit code
# has to say so, or anything running this in a script reads failure as success.
if bring_up; then
  summary
else
  summary
  exit 1
fi
