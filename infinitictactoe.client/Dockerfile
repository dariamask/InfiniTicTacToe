FROM node:18-alpine AS build
WORKDIR /app
COPY infinitictactoe.client/package*.json ./
RUN npm install
COPY infinitictactoe.client/ ./
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist/browser /usr/share/nginx/html
COPY infinitictactoe.client/nginx.conf /etc/nginx/nginx.conf
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
