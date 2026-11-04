# Build with the repository root as the context (the gateway references ../gateway-auth):
#   docker build -f gateway/Dockerfile -t gateway .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY gateway-auth/GatewayAuth.csproj gateway-auth/
COPY gateway/Gateway.csproj gateway/
RUN dotnet restore gateway/Gateway.csproj

COPY gateway-auth/ gateway-auth/
COPY gateway/ gateway/
RUN dotnet publish gateway/Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Gateway.dll"]
