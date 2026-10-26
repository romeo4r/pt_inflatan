# syntax=docker/dockerfile:1
# Multi-stage build with Next.js standalone output: the runtime image holds only server.js,
# the compiled pages and static assets (no node_modules tree, no source).

FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
# Public env vars are inlined at build time: the browser must reach the API through the host port.
ARG NEXT_PUBLIC_API_URL=http://localhost:5000
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_TELEMETRY_DISABLED=1
RUN npm run build

FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3800
ENV HOSTNAME=0.0.0.0

RUN addgroup -S nextjs && adduser -S nextjs -G nextjs
COPY --from=build --chown=nextjs:nextjs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nextjs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nextjs /app/public ./public

USER nextjs
EXPOSE 3800

# 127.0.0.1 on purpose: busybox wget resolves "localhost" to ::1 while Next.js listens on IPv4.
HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=5 CMD wget -qO- http://127.0.0.1:3800/standings > /dev/null || exit 1

CMD ["node", "server.js"]
