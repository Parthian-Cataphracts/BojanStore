# Build and run one of the two Next.js apps.
#
# The same file builds both — pass `--build-arg APP=storefront` or
# `--build-arg APP=admin`. The compose file does exactly that, so the two
# services differ only in that argument and their environment.

ARG NODE_VERSION=22-alpine

# ---------- dependencies ----------
FROM node:${NODE_VERSION} AS deps
WORKDIR /repo

# pnpm comes from the Node image's own corepack, pinned by the version in
# package.json, so the container and a developer's machine resolve the same
# tree from the same lockfile.
RUN corepack enable

# Manifests only. Everything below the install is invalidated when a
# dependency changes; nothing is invalidated when a component changes.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc ./
COPY apps/storefront/package.json apps/storefront/
COPY apps/admin/package.json apps/admin/
COPY packages/config/package.json packages/config/
COPY packages/ui/package.json packages/ui/

RUN pnpm install --frozen-lockfile

# ---------- build ----------
FROM node:${NODE_VERSION} AS build
WORKDIR /repo
RUN corepack enable

ARG APP
# Fail loudly here rather than producing a confusing empty image later.
RUN test -n "${APP}" || (echo "build-arg APP is required (storefront|admin)" && exit 1)

COPY --from=deps /repo/node_modules ./node_modules
COPY --from=deps /repo/apps/storefront/node_modules ./apps/storefront/node_modules
COPY --from=deps /repo/apps/admin/node_modules ./apps/admin/node_modules
COPY --from=deps /repo/packages/config/node_modules ./packages/config/node_modules
COPY --from=deps /repo/packages/ui/node_modules ./packages/ui/node_modules
COPY . .

# NEXT_PUBLIC_* values are substituted into the client bundle at build time, not
# read at boot — so they have to be arguments here. Changing one means
# rebuilding the image, which is why the API URL a browser uses is a deploy-time
# decision rather than a runtime one.
ARG NEXT_PUBLIC_API_BASE_URL
ARG NEXT_PUBLIC_SITE_URL
ARG NEXT_PUBLIC_USE_MOCK_DATA=false
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL} \
    NEXT_PUBLIC_SITE_URL=${NEXT_PUBLIC_SITE_URL} \
    NEXT_PUBLIC_USE_MOCK_DATA=${NEXT_PUBLIC_USE_MOCK_DATA} \
    NEXT_TELEMETRY_DISABLED=1 \
    # Turns on `output: 'standalone'` — see the note in next.config.mjs for why
    # it is opt-in rather than always on.
    BUILD_STANDALONE=1

RUN pnpm --filter "./apps/${APP}" build

# ---------- runtime ----------
FROM node:${NODE_VERSION} AS runtime
WORKDIR /app
ARG APP
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# The node image ships a `node` user; reuse it rather than adding another.
USER node

# The traced server, with the workspace layout it expects preserved. `static`
# and `public` are deliberately not part of the trace — Next expects to find
# them beside the server, and copies them itself only when it can.
COPY --chown=node:node --from=build /repo/apps/${APP}/.next/standalone ./
COPY --chown=node:node --from=build /repo/apps/${APP}/.next/static ./apps/${APP}/.next/static
COPY --chown=node:node --from=build /repo/apps/${APP}/public ./apps/${APP}/public

EXPOSE 3000

# Node's own fetch, so the image needs no curl or wget. `start-period` is
# generous because the first request also warms the server's route cache.
HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=5 \
  CMD node -e "fetch('http://127.0.0.1:3000/').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"

# `APP` is a build argument and so is gone by runtime; bake the resolved path
# into the command instead of trying to read it from the environment.
ENV SERVER_PATH=apps/${APP}/server.js
CMD ["sh", "-c", "node ${SERVER_PATH}"]
