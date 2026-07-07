FROM node:20-alpine AS build
WORKDIR /src

RUN corepack enable
COPY web/ ./web/

WORKDIR /src/web
RUN pnpm install --frozen-lockfile
RUN pnpm --filter @vben/web-ele... run build

FROM nginx:1.27-alpine
COPY deploy/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/web/apps/web-ele/dist /usr/share/nginx/html

EXPOSE 80
