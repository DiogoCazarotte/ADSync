# O Node compila somente o CSS; não faz parte da imagem final.
FROM node:22-bookworm-slim AS styles
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY ADSync.Client/ ADSync.Client/
RUN npm run build:css

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ADSync.Domain/ADSync.Domain.csproj ADSync.Domain/
COPY ADSync.Application/ADSync.Application.csproj ADSync.Application/
COPY ADSync.Infrastructure/ADSync.Infrastructure.csproj ADSync.Infrastructure/
COPY ADSync.Api/ADSync.Api.csproj ADSync.Api/
COPY ADSync.Client/ADSync.Client.csproj ADSync.Client/
RUN dotnet restore ADSync.Api/ADSync.Api.csproj \
    && dotnet restore ADSync.Client/ADSync.Client.csproj
COPY ADSync.Domain/ ADSync.Domain/
COPY ADSync.Application/ ADSync.Application/
COPY ADSync.Infrastructure/ ADSync.Infrastructure/
COPY ADSync.Api/ ADSync.Api/
COPY ADSync.Client/ ADSync.Client/
COPY --from=styles /src/ADSync.Client/wwwroot/css/app.css ADSync.Client/wwwroot/css/app.css
RUN dotnet publish ADSync.Api/ADSync.Api.csproj -c Release --no-restore -o /out/api -p:UseAppHost=false \
    && dotnet publish ADSync.Client/ADSync.Client.csproj -c Release --no-restore -o /out/client -p:SkipFrontendStyles=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /out/api/ ./
COPY --from=build /out/client/wwwroot/ ./client/
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Hosting__ServeClient=true
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ADSync.Api.dll"]
