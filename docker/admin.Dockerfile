# ── Dev target ────────────────────────────────────────────────────────────────
FROM node:22-alpine AS dev

WORKDIR /app

COPY admin/package.json admin/package-lock.json ./
RUN npm install

EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

# ── Build stage ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS build

WORKDIR /app

# Vite inlines VITE_* vars at build time, so the SSO endpoint must be baked in
# here rather than read at container start like the other services' env vars.
ARG VITE_SSO_URL=https://accounts.innovayse.com
ARG VITE_SSO_CLIENT_ID=hostpanel-admin
ENV VITE_SSO_URL=$VITE_SSO_URL
ENV VITE_SSO_CLIENT_ID=$VITE_SSO_CLIENT_ID

COPY admin/package.json admin/package-lock.json ./
RUN npm install

COPY admin/ .
RUN npm run build-only

# ── Prod target ───────────────────────────────────────────────────────────────
FROM nginx:alpine AS prod

COPY --from=build /app/dist /usr/share/nginx/html

# SPA fallback: serve index.html for all routes
RUN printf 'server {\n\
    listen 80;\n\
    root /usr/share/nginx/html;\n\
    index index.html;\n\
    location / {\n\
        try_files $uri $uri/ /index.html;\n\
    }\n\
}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
