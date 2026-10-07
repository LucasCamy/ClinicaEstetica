FROM node:26-alpine AS build
WORKDIR /app

ARG VITE_CLINIC_TIME_ZONE=America/Cuiaba
ENV VITE_CLINIC_TIME_ZONE=$VITE_CLINIC_TIME_ZONE

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.28-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
