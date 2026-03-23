# ============================================================
# Dockerfile — CEPLAN LoginApp
# Build multi-stage: SDK para compilar, aspnet para ejecutar
# Uso: docker-compose up --build
# ============================================================

# ── Etapa 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["LoginAppCore.csproj", "."]
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Etapa 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LoginAppCore.dll"]
