# Build with the repository root as the context (the gateway references common/platform-kit):
#   docker build -f core/gateway/Dockerfile -t gateway .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY common/platform-kit/PlatformKit.csproj common/platform-kit/
COPY core/gateway/Gateway.csproj core/gateway/
RUN dotnet restore core/gateway/Gateway.csproj

COPY common/platform-kit/ common/platform-kit/
COPY core/gateway/ core/gateway/
RUN dotnet publish core/gateway/Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Gateway.dll"]
