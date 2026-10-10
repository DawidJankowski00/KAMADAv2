# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet publish src/Kamada.Api/Kamada.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# libgomp1: OpenMP runtime needed by the whisper.cpp native libraries (Whisper.net.Runtime, runtimes/linux-x64).
# /app/models: speech model downloaded on first start (volume), writable by the non-root app user.
RUN apt-get update     && apt-get install -y --no-install-recommends libgomp1     && rm -rf /var/lib/apt/lists/*     && mkdir -p /app/models     && chown "$APP_UID" /app/models
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Kamada.Api.dll"]
