# Build and run the .NET API.
#
# Two stages: the SDK image compiles and publishes, the far smaller ASP.NET
# runtime image is what actually ships. Nothing from the SDK — no compilers, no
# NuGet cache, no source — survives into the final layer.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying the rest of the source, so editing a .cs file does not
# invalidate the layer holding a few hundred megabytes of NuGet packages. Only
# the files that can change the dependency graph come across first.
COPY Directory.Build.props Directory.Packages.props nuget.config Bojan.slnx ./
COPY src/Bojan.Domain/Bojan.Domain.csproj src/Bojan.Domain/
COPY src/Bojan.Application/Bojan.Application.csproj src/Bojan.Application/
COPY src/Bojan.Infrastructure/Bojan.Infrastructure.csproj src/Bojan.Infrastructure/
COPY src/Bojan.Api/Bojan.Api.csproj src/Bojan.Api/
RUN dotnet restore src/Bojan.Api/Bojan.Api.csproj

COPY src/ src/
RUN dotnet publish src/Bojan.Api/Bojan.Api.csproj \
    -c Release \
    -o /app \
    --no-restore \
    # The runtime image already carries the shared framework.
    --no-self-contained

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is what the compose healthcheck calls; the runtime image has no shell
# tooling of its own. postgresql-client is pg_dump, which is what screen 156's
# database backup actually runs — without it every backup job fails at the
# first step, and that failure is the whole point of taking it seriously rather
# than writing a manifest and calling it done.
#
# The version is pinned to the server's, from PostgreSQL's own apt repository,
# and that is the whole reason this is not one short `apt-get install` any more.
# Debian ships whatever major it shipped — 16 on this base — while compose runs
# `postgres:17-alpine`, and pg_dump refuses outright to dump a server newer
# than itself:
#
#   pg_dump: error: aborting because of server version mismatch
#   detail: server version: 17.10; pg_dump version: 16.14
#
# So every backup this deployment has ever taken failed, at the first step,
# with the archive deleted behind it. A dump has to be readable by the
# `pg_restore` of the server it is going back into, so the client tracks the
# server rather than the base image's calendar: change POSTGRES_MAJOR here and
# fontconfig comes along for the PDF reports. QuestPDF renders through
# SkiaSharp, which is a native library and looks for fontconfig even though the
# font it draws with is embedded in the assembly — without it the first PDF
# export dies inside the worker and the job is recorded as failed.
# in the compose image tag together, or this comes back.
ARG POSTGRES_MAJOR=17
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
 && install -d /usr/share/postgresql-common/pgdg \
 && curl --fail --silent --show-error --location \
      -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc \
      https://www.postgresql.org/media/keys/ACCC4CF8.asc \
 && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt $(. /etc/os-release && echo $VERSION_CODENAME)-pgdg main" \
      > /etc/apt/sources.list.d/pgdg.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends "postgresql-client-${POSTGRES_MAJOR}" \
 && apt-get install -y --no-install-recommends libfontconfig1 \
 && apt-get purge -y gnupg \
 && apt-get autoremove -y \
 && rm -rf /var/lib/apt/lists/*

# An unprivileged account, created here rather than relying on the image's
# default: a container that only ever reads its own binaries has no reason to
# run as root, and the two writable paths below are chowned to it explicitly.
RUN useradd --uid 64198 --create-home --shell /usr/sbin/nologin bojan

COPY --from=build /app ./

# Product media, backup archives and the log. Declared so they exist with the
# right owner even before compose mounts a volume over them — otherwise Docker
# creates them as root on first run and the API cannot write.
#
# `/data/logs` was missing from this list while compose mounted a volume there,
# which is the whole story of the log screen: Docker created the volume owned by
# root, the container runs as `bojan`, and the Serilog file sink cannot raise
# the failure anywhere — a sink that threw would take the application down over
# a log line, so it swallows the error and carries on writing to the console.
# The result was an installation that logged nothing to disk, a panel that
# correctly reported no files, and no clue anywhere as to why. The comment above
# has described this exact failure since the day it was written; the directory
# was simply not in the list.
RUN mkdir -p /data/uploads /data/backup-archives /data/logs /data/keys \
 && chown -R bojan:bojan /data

USER bojan

# Kestrel binds this; compose maps it. Plain HTTP on purpose — TLS terminates
# at the reverse proxy in front, which is where the certificate lives.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=5 \
  CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Bojan.Api.dll"]
