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
# tooling of its own.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# An unprivileged account, created here rather than relying on the image's
# default: a container that only ever reads its own binaries has no reason to
# run as root, and the two writable paths below are chowned to it explicitly.
RUN useradd --uid 64198 --create-home --shell /usr/sbin/nologin bojan

COPY --from=build /app ./

# Product media and backup archives. Declared so they exist with the right
# owner even before compose mounts a volume over them — otherwise Docker
# creates them as root on first run and the API cannot write.
RUN mkdir -p /data/uploads /data/backup-archives \
 && chown -R bojan:bojan /data

USER bojan

# Kestrel binds this; compose maps it. Plain HTTP on purpose — TLS terminates
# at the reverse proxy in front, which is where the certificate lives.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=5 \
  CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Bojan.Api.dll"]
