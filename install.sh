#!/usr/bin/env bash
#
# Bojan Store — bootstrap installer.
#
# Provisions a bare Ubuntu/Debian server with one command:
#
#   bash <(curl -Ls https://raw.githubusercontent.com/Parthian-Cataphracts/BojanStore/main/install.sh)
#
# This file only gets the source onto the machine; everything else lives in
# deploy/install.sh, which this hands over to. Keeping the entry point tiny is
# deliberate — it is fetched over the network and run as root, so it should be
# short enough to read in full before anyone does that.
#
# Already cloned the repository? Skip this and run:
#
#   sudo bash deploy/install.sh

set -euo pipefail

readonly REPO_SLUG='Parthian-Cataphracts/BojanStore'
readonly REPO_BRANCH='main'
readonly REPO_URL="https://github.com/${REPO_SLUG}.git"
readonly ARCHIVE_URL="https://codeload.github.com/${REPO_SLUG}/tar.gz/refs/heads/${REPO_BRANCH}"
readonly INSTALL_DIR="${BOJAN_DIR:-/opt/bojan}"

if [[ -t 1 ]]; then
  readonly BOLD=$'\033[1m' RED=$'\033[31m' GREEN=$'\033[32m' RESET=$'\033[0m'
else
  readonly BOLD='' RED='' GREEN='' RESET=''
fi

step() { printf '\n%s==>%s %s\n' "$BOLD" "$RESET" "$1"; }
info() { printf '    %s\n' "$1"; }
ok()   { printf '    %s%s%s\n' "$GREEN" "$1" "$RESET"; }
die()  { printf '\n%serror:%s %s\n\n' "$RED" "$RESET" "$1" >&2; exit 1; }

# --- preflight --------------------------------------------------------------

[[ "$(uname -s)" == "Linux" ]] ||
  die "This installer targets Linux. On macOS or Windows, clone the repository and run: docker compose up -d --build"

[[ "$(id -u)" -eq 0 ]] ||
  die "Run as root:  bash <(curl -Ls https://raw.githubusercontent.com/${REPO_SLUG}/${REPO_BRANCH}/install.sh)  — prefix it with sudo."

# deploy/install.sh asks questions. Under `curl | bash` stdin is the script
# itself, so those reads would silently consume the script's own text rather
# than wait for an answer. The process-substitution form in the header keeps
# stdin on the terminal, which is why it is the documented one.
[[ -t 0 ]] || die "No terminal on stdin. Use:  bash <(curl -Ls https://raw.githubusercontent.com/${REPO_SLUG}/${REPO_BRANCH}/install.sh)  rather than piping into bash."

# --- dependencies -----------------------------------------------------------

step "Checking prerequisites"

missing=()
for tool in git curl tar; do
  command -v "$tool" >/dev/null 2>&1 || missing+=("$tool")
done

if (( ${#missing[@]} )); then
  info "Installing: ${missing[*]}"
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
  apt-get install -y -qq ca-certificates "${missing[@]}" >/dev/null
  ok "Installed."
else
  ok "git, curl and tar are present."
fi

# --- fetch the source -------------------------------------------------------

# Three ways in, tried in order. A server behind a firewall that blocks the git
# protocol but allows plain HTTPS is common enough to be worth the fallback,
# and a half-extracted archive is worse than none — hence the structure check.
fetch_source() {
  if [[ -d "$INSTALL_DIR/.git" ]]; then
    step "Updating existing checkout at $INSTALL_DIR"
    if git -C "$INSTALL_DIR" fetch --depth 1 origin "$REPO_BRANCH" 2>/dev/null &&
       git -C "$INSTALL_DIR" reset --hard "origin/${REPO_BRANCH}" >/dev/null 2>&1; then
      ok "Updated to the latest $REPO_BRANCH."
      return 0
    fi
    info "Update failed; falling back to a fresh copy."
  fi

  step "Downloading Bojan Store"

  if git clone --depth 1 --branch "$REPO_BRANCH" "$REPO_URL" "$INSTALL_DIR" 2>/dev/null; then
    ok "Cloned to $INSTALL_DIR."
    return 0
  fi
  info "git clone did not work; trying the HTTPS archive."

  local tmp
  tmp="$(mktemp -d)"
  # shellcheck disable=SC2064  # expand $tmp now, not at trap time
  trap "rm -rf '$tmp'" RETURN

  if ! curl -fsSL "$ARCHIVE_URL" -o "$tmp/src.tar.gz"; then
    return 1
  fi

  tar -xzf "$tmp/src.tar.gz" -C "$tmp"
  local extracted
  extracted="$(find "$tmp" -maxdepth 1 -type d -name 'BojanStore-*' | head -1)"

  # A truncated download still extracts something; make sure it is the project.
  [[ -n "$extracted" && -d "$extracted/backend" && -d "$extracted/frontend" && -f "$extracted/deploy/install.sh" ]] ||
    return 1

  rm -rf "$INSTALL_DIR"
  mkdir -p "$(dirname "$INSTALL_DIR")"
  mv "$extracted" "$INSTALL_DIR"
  ok "Downloaded to $INSTALL_DIR."
}

if ! fetch_source; then
  if [[ -f "$INSTALL_DIR/deploy/install.sh" ]]; then
    info "Could not reach GitHub — continuing with the copy already at $INSTALL_DIR."
  else
    die "Could not download the source, and there is no usable copy at $INSTALL_DIR.
    On a machine with access, run:
      curl -fsSL $ARCHIVE_URL -o bojan.tar.gz
    then copy it over, extract it to $INSTALL_DIR, and run: sudo bash $INSTALL_DIR/deploy/install.sh"
  fi
fi

# --- hand over --------------------------------------------------------------

[[ -f "$INSTALL_DIR/deploy/install.sh" ]] ||
  die "$INSTALL_DIR/deploy/install.sh is missing — the download looks incomplete."

exec bash "$INSTALL_DIR/deploy/install.sh" "$@"
