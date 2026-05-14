# Build stage
FROM node:22-alpine AS build-stage
WORKDIR /app
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
ARG VITE_CLARITY_ID
ENV VITE_CLARITY_ID=$VITE_CLARITY_ID
COPY package*.json ./
RUN npm install
COPY . .
# Using vite build directly to skip vue-tsc (saves memory on 1GB RAM instances)
RUN npx vite build

# Production stage
FROM nginx:stable-alpine AS production-stage
COPY --from=build-stage /app/dist /usr/share/nginx/html
# Add custom nginx config for SPA routing
RUN echo 'server { \
    listen 80; \
    location / { \
        root /usr/share/nginx/html; \
        index index.html; \
        try_files $uri $uri/ /index.html; \
    } \
}' > /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
