# Build with the repository root as the context (it references ../gateway-auth):
#   docker build -f mock-api/Dockerfile -t mock-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY gateway-auth/GatewayAuth.csproj gateway-auth/
COPY mock-api/MockApi.csproj mock-api/
RUN dotnet restore mock-api/MockApi.csproj

COPY gateway-auth/ gateway-auth/
COPY mock-api/ mock-api/
RUN dotnet publish mock-api/MockApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MockApi.dll"]
