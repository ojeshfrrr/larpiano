FROM nginx:stable-alpine
COPY website/ /usr/share/nginx/html/
EXPOSE 80
