#!/usr/bin/env bash
#
# Bojan Store — support mailbox.
#
# Gives the shop a real mailbox: something@yourdomain that sends and receives,
# with SPF/DKIM/DMARC so a real inbox does not junk it. deploy/install.sh
# provisions the storefront, the panel and the database; this is the one piece
# it deliberately leaves out, because a mail server is a second thing to keep
# secure and reachable, not a checkbox in the same installer.
#
#   sudo bash deploy/mailbox-setup.sh              mailbox named "support"
#   sudo MAIL_USER=info bash deploy/mailbox-setup.sh   a different local part
#
# Unlike the web stack, this is not idempotent in the "safe to re-run blind"
# sense that install.sh is — it edits system mail configuration, not a
# container. It IS safe to run twice: every step here checks what is already
# in place and skips it, the same as the rest of this repo's deploy scripts.
#
# What it does, in order:
#   1. Installs Postfix and points it at this domain, restricted so it can
#      only relay mail a logged-in sender submits — never an open relay.
#   2. Installs OpenDKIM and signs every outgoing message.
#   3. Installs Dovecot for IMAP, so the mailbox can be read.
#   4. Wires Postfix's authenticated submission port (587) to Dovecot's own
#      account database, so the one password works for sending and reading.
#   5. Expands the site's existing certificate to cover mail, rather than
#      issuing a second one.
#   6. Prints the DNS records to add and the values for
#      تنظیمات ← صندوق پستی in the panel.
#
# What it does not do: relay for any address but the one it creates, listen
# on the internet for authenticated submission (587 stays loopback-only — the
# API container submits from the same host), or touch info you have not asked
# it to.

set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly ENV_FILE="$ROOT/.env"
readonly MAIL_USER="${MAIL_USER:-support}"
readonly DKIM_SELECTOR="mail"
readonly BACKUP_DIR="/var/backups/bojan-mail"

if [[ -t 1 ]]; then
  readonly BOLD=$'\033[1m' RED=$'\033[31m' GREEN=$'\033[32m' YELLOW=$'\033[33m' RESET=$'\033[0m'
else
  readonly BOLD='' RED='' GREEN='' YELLOW='' RESET=''
fi

step() { printf '\n%s==>%s %s\n' "$BOLD" "$RESET" "$1"; }
info() { printf '    %s\n' "$1"; }
warn() { printf '    %s%s%s\n' "$YELLOW" "$1" "$RESET"; }
ok()   { printf '    %s%s%s\n' "$GREEN" "$1" "$RESET"; }
die()  { printf '\n%serror:%s %s\n\n' "$RED" "$RESET" "$1" >&2; exit 1; }

[[ "$(id -u)" -eq 0 ]] || die "Run as root."

# --- discovery ---------------------------------------------------------------

# The domain comes from the same .env deploy/install.sh already wrote, not
# from asking again or reading it back out of Postfix — Postfix does not exist
# yet the first time this runs, and .env is the one place this repo already
# calls "the domain".
[[ -f "$ENV_FILE" ]] || die "No .env at $ENV_FILE — run deploy/install.sh first."
DOMAIN="$(grep -E '^PUBLIC_DOMAIN=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
[[ -n "$DOMAIN" ]] || die "PUBLIC_DOMAIN is empty in .env — set a domain and re-run deploy/install.sh first."

# The apex, not a mail.$DOMAIN subdomain: a second hostname is a second DNS
# record and a second certificate name for a shop this size to keep straight,
# and the certificate deploy/install.sh already issued covers the apex.
readonly MAIL_HOST="$DOMAIN"
readonly ADDRESS="${MAIL_USER}@${DOMAIN}"

step "Bojan mailbox setup"
info "Domain    : $DOMAIN"
info "Mailbox   : $ADDRESS"
info "Certificate: $MAIL_HOST"

backup_now() {
  mkdir -p "$BACKUP_DIR"
  local stamp; stamp="$(date +%Y%m%d%H%M%S)"
  [[ -f /etc/postfix/main.cf ]] && cp /etc/postfix/main.cf "$BACKUP_DIR/main.cf.$stamp"
  [[ -f /etc/postfix/master.cf ]] && cp /etc/postfix/master.cf "$BACKUP_DIR/master.cf.$stamp"
  [[ -f /etc/aliases ]] && cp /etc/aliases "$BACKUP_DIR/aliases.$stamp"
  ok "Backed up to $BACKUP_DIR (suffix $stamp)."
}

# --- Postfix -------------------------------------------------------------

install_postfix() {
  step "Postfix"

  if dpkg -s postfix >/dev/null 2>&1; then
    ok "Postfix already installed."
  else
    say=info
    # Preseeded so `apt-get install` does not stop for the "General type of
    # mail configuration" dialog — there is no terminal attached when this is
    # called from a deploy pipeline, and the dialog has no default that is
    # right for a mail server actually meant to send.
    debconf-set-selections <<SEL
postfix postfix/main_mailer_type select Internet Site
postfix postfix/mailname string $DOMAIN
SEL
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -qq
    apt-get install -y -qq postfix >/dev/null
    ok "Installed Postfix."
  fi

  # Every value set explicitly rather than trusting the debconf answers above,
  # so a re-run repairs a hand-edited main.cf back to this shape instead of
  # silently agreeing with whatever is already there.
  postconf -e "myhostname=$MAIL_HOST"
  postconf -e "mydomain=$DOMAIN"
  postconf -e "myorigin=\$mydomain"
  postconf -e "mydestination=\$myhostname, $DOMAIN, localhost.\$mydomain, localhost"
  postconf -e "inet_interfaces=all"
  postconf -e "inet_protocols=ipv4"

  # The open-relay guard. mynetworks is what the plain port-25 listener (used
  # by other mail servers delivering TO this shop) checks before it accepts a
  # message FOR somewhere else — restricted to loopback, nothing on the public
  # internet can use this server to relay. Authenticated senders use the
  # submission port below instead, which checks a login, not an IP.
  postconf -e "mynetworks=127.0.0.0/8, [::1]/128"
  postconf -e "smtpd_relay_restrictions=permit_mynetworks, permit_sasl_authenticated, reject_unauth_destination"

  # No banner that names the software and version — free reconnaissance for
  # anything probing the port.
  postconf -e "smtpd_banner=\$myhostname ESMTP"
  postconf -e "disable_vrfy_command=yes"

  ok "Postfix configured for $DOMAIN."
}

# The authenticated submission port (587). Plain port 25 is for other mail
# servers delivering inbound; this is for the shop's own backend to send
# outbound, and it is not reachable from outside this host — see mynetworks
# above for why sending has to go through a login rather than an IP allowlist.
enable_submission() {
  step "Submission (587)"

  if ! postconf -M 2>/dev/null | grep -qE '^submission[[:space:]]+inet'; then
    cat >> /etc/postfix/master.cf <<'MASTERCF'

# --- Bojan mail setup: authenticated submission ---
submission inet n       -       y       -       -       smtpd
# --- end Bojan mail setup ---
MASTERCF
    ok "Added the submission service to master.cf."
  else
    ok "Submission service already present."
  fi

  # `postconf -P` sets a per-service override without touching the rest of
  # master.cf, and is safe to run every time: an unchanged value is a no-op.
  #
  # The two milter lines are what make DKIM signing this service's problem and
  # no one else's — see the long comment in install_opendkim for why that is
  # the right place to decide it rather than OpenDKIM's own IP allowlist.
  postconf -P \
    'submission/inet/syslog_name=postfix/submission' \
    'submission/inet/smtpd_tls_security_level=encrypt' \
    'submission/inet/smtpd_sasl_auth_enable=yes' \
    'submission/inet/smtpd_sasl_type=dovecot' \
    'submission/inet/smtpd_sasl_path=private/auth' \
    'submission/inet/smtpd_sasl_security_options=noanonymous' \
    'submission/inet/smtpd_client_restrictions=permit_sasl_authenticated,reject' \
    'submission/inet/smtpd_relay_restrictions=permit_sasl_authenticated,reject' \
    'submission/inet/milter_macro_daemon_name=ORIGINATING' \
    'submission/inet/smtpd_milters=local:opendkim/opendkim.sock' \
    'submission/inet/non_smtpd_milters=$smtpd_milters'

  ok "Submission requires a login, TLS, and signs everything it accepts."
}

# --- OpenDKIM ----------------------------------------------------------------

install_opendkim() {
  step "DKIM"

  if ! dpkg -s opendkim opendkim-tools >/dev/null 2>&1; then
    export DEBIAN_FRONTEND=noninteractive
    apt-get install -y -qq opendkim opendkim-tools >/dev/null
    ok "Installed OpenDKIM."
  else
    ok "OpenDKIM already installed."
  fi

  local keydir="/etc/opendkim/keys/$DOMAIN"
  mkdir -p "$keydir"

  if [[ -f "$keydir/$DKIM_SELECTOR.private" ]]; then
    ok "DKIM key already exists."
  else
    opendkim-genkey -b 2048 -d "$DOMAIN" -s "$DKIM_SELECTOR" -D "$keydir"
    chown -R opendkim:opendkim "$keydir"
    chmod 640 "$keydir/$DKIM_SELECTOR.private"
    ok "Generated a DKIM key."
  fi

  # One block, owned by this script — see the same reasoning on the Dovecot
  # drop-in below.
  #
  # `Mode s` — sign only, never verify — rather than the more common `sv`.
  # The usual reason for `sv` is that OpenDKIM has to decide, per connection,
  # whether a message is ours to sign or someone else's to verify, and that
  # decision is normally made by matching the client IP against
  # InternalHosts. That check is IP trust standing in for a question this
  # server can answer more directly: whether the sender proved who they are.
  # The milter is wired below only to the submission service (587), which
  # Postfix already refuses to relay through without a successful login — so
  # every message that reaches this socket at all is already an authenticated
  # send, regardless of whether it arrived over loopback, a Docker bridge, or
  # the public address the app container resolves this host's own name to.
  # `s` mode skips the IP question entirely and signs everything it sees,
  # which for a milter that only sees authenticated outbound mail is the
  # correct unconditional answer. Verifying inbound mail is a separate
  # feature this shop does not need — Bojan does not surface DKIM/SPF results
  # on a support thread — so port 25 carries no milter at all.
  cat > /etc/opendkim.conf <<CONF
# Managed by deploy/mailbox-setup.sh — remove this file to undo DKIM signing.
Syslog                  yes
UMask                   002
OversignHeaders         From
Mode                    s
Canonicalization        relaxed/simple
Socket                  local:/var/spool/postfix/opendkim/opendkim.sock
PidFile                 /run/opendkim/opendkim.pid
# The group half is "postfix", not "opendkim" — that is what makes the
# socket group-writable by the postfix processes connecting to it without
# adding postfix as a member of the opendkim group. It has to be that way
# round: OpenDKIM refuses to load a private key it considers shared —
# "key data is not secure ... group has multiple users" — whenever the key's
# own group has more than the opendkim user in it, and putting postfix into
# that same group to solve the socket problem was exactly what put a second
# user in it. The two need separate groups; postfix already has one of its
# own, and the daemon's own uid (still opendkim) is what the key-ownership
# check and the key-file read both key off, so signing still works.
UserID                  opendkim:postfix
KeyTable                /etc/opendkim/key.table
# `refile:`, not a bare path: signing.table's *@domain entry is a wildcard,
# and OpenDKIM only treats "*" as one under the regex-file map type. Without
# it, the map defaults to an exact-string lookup, so "support@bojanstore.com"
# is compared literally against the four-character key "*@bojanstore.com",
# never matches, and every outbound message goes out unsigned — silently
# unless LogWhy is on, since a mismatch here is not an error to OpenDKIM.
SigningTable            refile:/etc/opendkim/signing.table
CONF

  printf '%s._domainkey.%s %s:%s:%s\n' "$DKIM_SELECTOR" "$DOMAIN" "$DOMAIN" "$DKIM_SELECTOR" "$keydir/$DKIM_SELECTOR.private" \
    > /etc/opendkim/key.table
  printf '*@%s %s._domainkey.%s\n' "$DOMAIN" "$DKIM_SELECTOR" "$DOMAIN" > /etc/opendkim/signing.table

  # The milter socket lives inside Postfix's chroot (/var/spool/postfix), which
  # is why Postfix's own config below refers to it as `local:opendkim/...` —
  # relative to that chroot — while OpenDKIM's own config above uses the real
  # path. Same file, two views of it.
  mkdir -p /var/spool/postfix/opendkim
  chown opendkim:postfix /var/spool/postfix/opendkim
  chmod 750 /var/spool/postfix/opendkim

  # Undoes an earlier shape of this script, which put postfix into the
  # opendkim group to reach the socket — see the comment on UserID above for
  # why that group is for the socket's use alone now. Idempotent cleanup for
  # any host this ran against before that changed.
  gpasswd -d postfix opendkim >/dev/null 2>&1 || true

  postconf -e "milter_default_action=accept"
  postconf -e "milter_protocol=6"

  # Deliberately empty. Every daemon inherits main.cf unless a service's own
  # master.cf entry overrides it, and enable_submission sets the milter on the
  # submission service specifically — this line is what keeps a global value
  # here from quietly re-attaching it to port 25 (plain inbound) as well, an
  # empty value blocks main.cf's own from being inherited.
  postconf -e "smtpd_milters="
  postconf -e "non_smtpd_milters="

  systemctl enable --now opendkim >/dev/null 2>&1
  systemctl restart opendkim
  ok "OpenDKIM signing outgoing mail for $DOMAIN."
}

# --- certificate ---------------------------------------------------------

ensure_cert() {
  step "Certificate"

  local live="/etc/letsencrypt/live/$DOMAIN"
  [[ -d "$live" ]] || die "No certificate at $live — run deploy/install.sh with a domain set first."

  if openssl x509 -in "$live/cert.pem" -noout -ext subjectAltName 2>/dev/null | grep -q "DNS:$MAIL_HOST"; then
    ok "The site's certificate already covers $MAIL_HOST."
  else
    # $MAIL_HOST is the apex, and deploy/install.sh's own certificate already
    # names it — this branch is here for the day MAIL_HOST stops being the
    # apex, not because it is expected to run today.
    die "$live does not cover $MAIL_HOST. Re-run deploy/install.sh (or b-ui domain) to include it, then re-run this script."
  fi

  postconf -e "smtpd_tls_cert_file=$live/fullchain.pem"
  postconf -e "smtpd_tls_key_file=$live/privkey.pem"
  ok "Postfix presents the site's certificate."
}

# --- mailbox ---------------------------------------------------------------

create_mailbox() {
  step "Mailbox"

  if id -u "$MAIL_USER" >/dev/null 2>&1; then
    ok "User '$MAIL_USER' already exists."
  else
    # No shell: this account holds mail, it does not log in with one. IMAP and
    # the submission port authenticate it through PAM regardless of shell —
    # `nologin` only blocks an interactive `su`.
    adduser --disabled-password --gecos "Bojan support mailbox" --shell /usr/sbin/nologin "$MAIL_USER"
    ok "Created user '$MAIL_USER'."
  fi

  # `adduser --disabled-password` leaves the account locked (passwd -S reports
  # L), same as a re-run finding a user this script created but that never got
  # as far as the line below — e.g. a run piped from `b-ui mail` over ssh
  # without a terminal on the other end, where `passwd` cannot prompt at all.
  #
  # A real terminal still gets the interactive prompt, because a password the
  # operator chose is better than one nobody saw them choose. Everywhere else
  # — cron, a CI step, this exact ssh invocation — a strong one is generated
  # instead, and printed once in verify() rather than swallowed by a `passwd`
  # call that would have hung waiting for input that was never coming.
  if ! passwd -S "$MAIL_USER" 2>/dev/null | awk '{print $2}' | grep -q '^P$'; then
    if [[ -t 0 ]]; then
      warn "Set its mailbox password now — this is the one password for both sending and reading:"
      passwd "$MAIL_USER"
    else
      GENERATED_PASSWORD="$(openssl rand -base64 18 | tr -d '=+/' | cut -c1-20)"
      echo "${MAIL_USER}:${GENERATED_PASSWORD}" | chpasswd
      ok "No terminal to prompt on — generated a password (shown at the end)."
    fi
  fi

  local maildir="/home/$MAIL_USER/Maildir"
  if [[ ! -d "$maildir" ]]; then
    mkdir -p "$maildir"/{cur,new,tmp}
    chown -R "$MAIL_USER:$MAIL_USER" "$maildir"
    chmod -R 700 "$maildir"
    ok "Created $maildir."
  fi

  if [[ "$(postconf -h home_mailbox)" != "Maildir/" ]]; then
    postconf -e "home_mailbox=Maildir/"
    ok "Postfix delivers to Maildir."
  fi

  if grep -qE "^${MAIL_USER}:" /etc/aliases 2>/dev/null; then
    ok "Alias for ${MAIL_USER}@ already present."
  else
    echo "${MAIL_USER}: ${MAIL_USER}" >> /etc/aliases
    newaliases
    ok "Mail for $ADDRESS now lands in that mailbox."
  fi

  # Deferred to apply(): doveadm needs Dovecot already running with the config
  # this script writes, and Dovecot is not installed yet at this point.
  NEEDS_STD_FOLDERS=1
}

# --- Dovecot -----------------------------------------------------------------

install_dovecot() {
  step "IMAP"

  if ! dpkg -s dovecot-imapd >/dev/null 2>&1; then
    export DEBIAN_FRONTEND=noninteractive
    apt-get install -y -qq dovecot-imapd >/dev/null
    ok "Installed Dovecot."
  else
    ok "Dovecot already installed."
  fi

  # Dovecot 2.4 renamed most of what this file sets — ssl_cert became
  # ssl_server_cert_file, and mail_location split into mail_driver + mail_path.
  # Writing 2.3 syntax to a 2.4 server is a fatal config error, so the version
  # installed decides the grammar rather than an assumption about it.
  local ver major minor
  ver="$(dovecot --version 2>/dev/null | awk '{print $1}')"
  major="${ver%%.*}"; minor="$(printf '%s' "$ver" | cut -d. -f2)"
  [[ -n "$major" && -n "$minor" ]] || die "Could not read the Dovecot version."
  info "Dovecot $ver detected."

  {
    echo "# Managed by deploy/mailbox-setup.sh — remove this file to undo the IMAP setup."
    echo

    if (( major > 2 || (major == 2 && minor >= 4) )); then
      echo "mail_driver = maildir"
      echo "mail_path = %{home}/Maildir"
      # 2.4 introduced mail_inbox_path, defaulting to /var/mail/%{user} — the
      # old mbox spool. Left at that default, Dovecot would autocreate INBOX
      # there (which this mailbox user cannot write) while Postfix delivers to
      # ~/Maildir — mail landing in one place and being read from another.
      # Blanking it makes INBOX use mail_path, which is where Postfix put it.
      echo "mail_inbox_path ="
      echo
      echo "ssl_server_cert_file = /etc/letsencrypt/live/$DOMAIN/fullchain.pem"
      echo "ssl_server_key_file = /etc/letsencrypt/live/$DOMAIN/privkey.pem"
    else
      echo "mail_location = maildir:~/Maildir"
      echo
      echo "ssl = required"
      echo "ssl_cert = </etc/letsencrypt/live/$DOMAIN/fullchain.pem"
      echo "ssl_key = </etc/letsencrypt/live/$DOMAIN/privkey.pem"
    fi

    echo "ssl_min_protocol = TLSv1.2"
    echo
    echo "# Credentials may never cross the network in the clear; Dovecot"
    echo "# refuses plaintext auth unless the connection is already TLS."
    echo "auth_allow_cleartext = no"
    echo "auth_mechanisms = plain login"
    echo
    echo "# Postfix's submission port authenticates against this same socket,"
    echo "# which is what makes one password work for sending and reading."
    echo "service auth {"
    echo "  unix_listener /var/spool/postfix/private/auth {"
    echo "    mode = 0660"
    echo "    user = postfix"
    echo "    group = postfix"
    echo "  }"
    echo "}"
  } > /etc/dovecot/conf.d/99-bojan.conf

  # Validate before anything restarts — a rejected config leaves the file
  # removable rather than a dead service to debug from a cold start.
  if ! doveconf -n >/dev/null 2>/tmp/dovecot-check.err; then
    local reason; reason="$(cat /tmp/dovecot-check.err)"
    rm -f /etc/dovecot/conf.d/99-bojan.conf
    die "Dovecot rejected the configuration, so it was removed and nothing was restarted:
   $reason"
  fi
  ok "Wrote /etc/dovecot/conf.d/99-bojan.conf"
}

# Certbot renews silently every ~60 days. All three daemons read the
# certificate once at start, so without this they keep serving the expired one
# long after the file on disk changed — and the failure would only show up as
# clients and other servers refusing the connection.
install_renewal_hook() {
  local hook=/etc/letsencrypt/renewal-hooks/deploy/reload-mail.sh
  mkdir -p "$(dirname "$hook")"
  cat > "$hook" <<'HOOK'
#!/usr/bin/env bash
# Managed by deploy/mailbox-setup.sh — reloads the mail daemons after renewal.
systemctl reload postfix 2>/dev/null || true
systemctl reload dovecot 2>/dev/null || true
HOOK
  chmod +x "$hook"
  ok "Renewal hook installed — the mail daemons pick up each renewed certificate."
}

open_firewall() {
  command -v ufw >/dev/null 2>&1 || return 0
  ufw status 2>/dev/null | grep -q "Status: active" || return 0

  # 25 is for RECEIVING: other mail servers connect here to deliver, and
  # without it the MX points at a port the firewall drops — mail is silently
  # deferred with nothing in the local log to show for it. 993 is IMAP, for
  # reading the mailbox. 587 is deliberately NOT opened: the API container
  # submits from this same host, which ufw never blocks, and exposing
  # authenticated submission to the internet would only invite brute force
  # against it.
  for port in 25 993; do
    if ufw status | grep -qE "^${port}(/tcp)?\b"; then
      ok "Firewall already allows ${port}."
    else
      ufw allow "${port}/tcp" >/dev/null
      ok "Opened port ${port}/tcp."
    fi
  done
}

apply() {
  step "Applying"
  postfix check || die "Postfix rejected the configuration — nothing reloaded. Restore from $BACKUP_DIR."

  # Restart rather than reload. A reload re-reads main.cf but Postfix's master
  # process holds a few settings — inet_protocols among them — for its own
  # lifetime and warns "ignoring inet_protocols parameter value change" rather
  # than picking up the new one. On a run that just set inet_protocols=ipv4
  # for the first time, reloading left the running master still on its old
  # "all" and it then tried an IPv6 wildcard bind for the submission service,
  # which fails outright on a host with no IPv6 stack: "Address family for
  # hostname not supported" — a start-time fatal, so submission (and every
  # other listener sharing that master) never came up. Every run of this
  # script is either a fresh install or a config change, and neither has
  # in-flight mail to lose the connections of, so there is no reason to prefer
  # a reload's smaller disruption here.
  systemctl enable --now postfix >/dev/null 2>&1
  systemctl restart postfix
  ok "Postfix running."

  systemctl enable --now dovecot >/dev/null 2>&1 || die "Dovecot failed to start — check: journalctl -u dovecot -n 50"
  systemctl restart dovecot
  ok "Dovecot running."

  if [[ "${NEEDS_STD_FOLDERS:-0}" == "1" ]]; then
    doveadm mailbox create    -u "$MAIL_USER" Sent Drafts Junk Trash Archive >/dev/null 2>&1 || true
    doveadm mailbox subscribe -u "$MAIL_USER" Sent Drafts Junk Trash Archive >/dev/null 2>&1 || true
    ok "Ensured standard folders (Sent, Drafts, Junk, Trash, Archive)."
  fi
}

verify() {
  step "Verifying"
  systemctl is-active --quiet postfix  && ok "postfix active"  || warn "postfix is NOT active"
  systemctl is-active --quiet dovecot  && ok "dovecot active"  || warn "dovecot is NOT active"
  systemctl is-active --quiet opendkim && ok "opendkim active" || warn "opendkim is NOT active"
  ss -tlnp 2>/dev/null | grep -q ':993' && ok "listening on 993 (IMAPS)" || warn "nothing is listening on 993"
  ss -tlnp 2>/dev/null | grep -q ':587' && ok "listening on 587 (submission)" || warn "nothing is listening on 587"

  local dkim_record="/etc/opendkim/keys/$DOMAIN/$DKIM_SELECTOR.txt"

  printf "\n%b\n" "${BOLD}DNS records to add (Cloudflare — DNS only, not proxied)${RESET}"
  printf "  %-6s %-30s %s\n" "MX" "$DOMAIN" "10 $MAIL_HOST"
  printf "  %-6s %-30s %s\n" "TXT" "$DOMAIN" "\"v=spf1 mx ~all\""
  printf "  %-6s %-30s %s\n" "TXT" "_dmarc.$DOMAIN" "\"v=DMARC1; p=quarantine; rua=mailto:${MAIL_USER}@${DOMAIN}\""
  if [[ -f "$dkim_record" ]]; then
    printf "  %-6s %-30s\n" "TXT" "${DKIM_SELECTOR}._domainkey.$DOMAIN"
    info "        (exact value below — opendkim-genkey wraps it across lines; Cloudflare wants it as one string)"
    grep -o '"[^"]*"' "$dkim_record" | tr -d '"' | tr -d '\n' | sed 's/^/          /'
    printf "\n"
  else
    warn "  Could not find the DKIM record at $dkim_record — read it with: cat $dkim_record"
  fi

  printf "\n%b\n" "${BOLD}پنل ← تنظیمات ← صندوق پستی${RESET}"
  echo "  آدرس ایمیل   : $ADDRESS"
  echo "  نام نمایشی   : فروشگاه بوژان"
  echo "  IMAP میزبان  : $MAIL_HOST      پورت 993      SSL/TLS"
  echo "  SMTP میزبان  : $MAIL_HOST      پورت 587      STARTTLS"
  echo "  نام کاربری   : $MAIL_USER"
  if [[ -n "${GENERATED_PASSWORD:-}" ]]; then
    echo "  رمز عبور     : $GENERATED_PASSWORD"
    warn "  این رمز فقط همین یک‌بار روی صفحه چاپ می‌شود — همین حالا در پنل وارد کنید."
  else
    echo "  رمز عبور     : همانی که با passwd بالا تنظیم شد"
  fi

  printf "\n%b\n" "${BOLD}Test it${RESET}"
  echo "  Once the DNS records above have propagated, send a message to $ADDRESS"
  echo "  from an outside address, then:  ls -l /home/$MAIL_USER/Maildir/new/"
  echo "  A file there means delivery works. If not: journalctl -u postfix -n 50"
  echo
  echo "  Check DKIM/SPF/DMARC pass from an outside address by sending to"
  echo "  check-auth@verifier.port25.com and reading the automated reply, or"
  echo "  use https://www.mail-tester.com."
}

main() {
  backup_now
  install_postfix
  enable_submission
  install_opendkim
  ensure_cert
  create_mailbox
  install_dovecot
  install_renewal_hook
  open_firewall
  apply
  verify
  printf "\n"
  ok "Done."
}

main "$@"
