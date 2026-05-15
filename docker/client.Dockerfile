# ── Dev target ────────────────────────────────────────────────────────────────
FROM node:22-alpine AS dev

RUN corepack enable && corepack prepare yarn@1.22.22 --activate

WORKDIR /app

COPY client/package.json client/yarn.lock ./
RUN yarn install --frozen-lockfile

EXPOSE 3000

CMD ["yarn", "dev", "--host", "0.0.0.0"]

# ── Build stage ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS build

RUN corepack enable && corepack prepare yarn@1.22.22 --activate

WORKDIR /app

COPY client/package.json client/yarn.lock ./
RUN yarn install --frozen-lockfile

COPY client/ .
RUN yarn build

# ── Prod target ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS prod

WORKDIR /app

COPY --from=build /app/.output .output/

ENV NUXT_HOST=0.0.0.0
ENV NUXT_PORT=3000
EXPOSE 3000

CMD ["node", ".output/server/index.mjs"]
