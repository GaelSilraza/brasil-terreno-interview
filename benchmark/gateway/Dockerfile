FROM node:24-alpine AS build
WORKDIR /app
COPY finance-front/package.json finance-front/package-lock.json finance-front/.npmrc ./
RUN npm ci
COPY finance-front/ ./
ENV VITE_API_URL=/api
RUN npm run build

FROM nginx:stable-alpine
COPY benchmark/gateway/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
