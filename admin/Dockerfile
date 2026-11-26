# Context: project root (compose: context ..), so prod can take its nginx config from docker/nginx/.
# Targets: dev (vite), build, prod (nginx).
FROM node:22.23.4-alpine3.24@sha256:fe4f6d74e20370acb04ad889077d14d0f64a9c8ea6a42bc60cddf1e844699914 AS dev

WORKDIR /app

COPY admin/package.json admin/package-lock.json ./
RUN npm ci

# The dev server's port; the prod image below does not expose it.
EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

FROM node:22.23.4-alpine3.24@sha256:fe4f6d74e20370acb04ad889077d14d0f64a9c8ea6a42bc60cddf1e844699914 AS build

WORKDIR /app

# Nothing environment-specific is baked in: the storefront and documentation addresses come from the environment at start.

COPY admin/package.json admin/package-lock.json ./
RUN npm ci

COPY admin/ .
RUN npm run build-only

# The unprivileged nginx build: it runs as the `nginx` user (uid 101) and keeps its pid and caches in
# folders that user owns. It listens on 80 like any nginx; inside a container that needs no privilege
# once net.ipv4.ip_unprivileged_port_start is 0, which the compose files set explicitly.
FROM nginxinc/nginx-unprivileged:1.31.7-alpine3.24@sha256:b5047b9b76845eac124358373c6852377c52e4b05834a47647e955dd91cb8f1e AS prod

# The start script writes config.js and edits index.html in place, so the web root belongs to the nginx user.
USER root
COPY --from=build --chown=101:0 /app/dist /usr/share/nginx/html
RUN chown 101:0 /usr/share/nginx/html

COPY docker/nginx/admin.conf /etc/nginx/conf.d/default.conf
COPY docker/nginx/snippets/security-headers.conf docker/nginx/snippets/admin-csp.conf /etc/nginx/snippets/

# At start, /docker-entrypoint.d/40-admin-runtime-config.sh writes config.js from CLIENT_BASE_URL and
# DOCS_URL (both optional; the script refuses a malformed value).
COPY docker/nginx/admin-config.js.template /etc/admin/config.js.template
COPY --chmod=755 docker/nginx/admin-entrypoint.sh /docker-entrypoint.d/40-admin-runtime-config.sh

USER 101

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:80/config.js || exit 1

CMD ["nginx", "-g", "daemon off;"]
