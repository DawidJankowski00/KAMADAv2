# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Shared/Kamada.Shared.Contracts/ src/Shared/Kamada.Shared.Contracts/
COPY src/Shared/Kamada.Shared.Matching/ src/Shared/Kamada.Shared.Matching/
COPY src/Web/Kamada.Web.Shared/ src/Web/Kamada.Web.Shared/
COPY src/Web/Kamada.Web.Admin/ src/Web/Kamada.Web.Admin/
COPY src/Web/Kamada.MobileApp/ src/Web/Kamada.MobileApp/
COPY benchmarks/Kamada.Assistant.Evals/cases.json benchmarks/Kamada.Assistant.Evals/cases.json
# On-device assistant: pinned browser libraries (WebLLM, transformers.js, ONNX Runtime), verified by checksum.
COPY deploy/assistant/fetch-runtime.sh deploy/assistant/fetch-runtime.sh
RUN sh deploy/assistant/fetch-runtime.sh src/Web/Kamada.MobileApp/wwwroot/assistant/vendor
RUN dotnet publish src/Web/Kamada.MobileApp/Kamada.MobileApp.csproj -c Release -o /app/publish

FROM nginx:1.29-alpine AS runtime
COPY deploy/nginx/spa.conf /etc/nginx/conf.d/default.conf
COPY deploy/nginx/40-kamada-config.sh /docker-entrypoint.d/40-kamada-config.sh
RUN chmod +x /docker-entrypoint.d/40-kamada-config.sh
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
