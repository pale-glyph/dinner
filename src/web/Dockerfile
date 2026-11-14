FROM node:24-bookworm-slim AS dependencies
WORKDIR /app
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci

FROM dependencies AS build
COPY src/web/ ./
RUN npm run build

FROM node:24-bookworm-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    HOSTNAME=0.0.0.0 \
    PORT=3000
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
USER node
EXPOSE 3000
CMD ["node", "server.js"]
