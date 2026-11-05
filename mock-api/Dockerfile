# Build with the repository root as the context (it references ../platform-kit):
#   docker build -f mock-api/Dockerfile -t mock-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY platform-kit/PlatformKit.csproj platform-kit/
COPY mock-api/MockApi.csproj mock-api/
RUN dotnet restore mock-api/MockApi.csproj

COPY platform-kit/ platform-kit/
COPY mock-api/ mock-api/
RUN dotnet publish mock-api/MockApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MockApi.dll"]
