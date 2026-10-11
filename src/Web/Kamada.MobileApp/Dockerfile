# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Shared/Kamada.Shared.Contracts/ src/Shared/Kamada.Shared.Contracts/
COPY src/Web/Kamada.Web.Shared/ src/Web/Kamada.Web.Shared/
COPY src/Web/Kamada.MobileApp/ src/Web/Kamada.MobileApp/
RUN dotnet publish src/Web/Kamada.MobileApp/Kamada.MobileApp.csproj -c Release -o /app/publish

FROM nginx:1.29-alpine AS runtime
COPY deploy/nginx/spa.conf /etc/nginx/conf.d/default.conf
COPY deploy/nginx/40-kamada-config.sh /docker-entrypoint.d/40-kamada-config.sh
RUN chmod +x /docker-entrypoint.d/40-kamada-config.sh
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
