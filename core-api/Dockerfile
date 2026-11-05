# Build with the repository root as the context (it references ../platform-kit):
#   docker build -f core-api/Dockerfile --target api -t core-api .                (the API)
#   docker build -f core-api/Dockerfile --target migrate -t core-api-migrate .    (one-shot database migration job)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY platform-kit/PlatformKit.csproj platform-kit/
COPY core-api/CoreApi.csproj core-api/dotnet-tools.json core-api/
RUN dotnet restore core-api/CoreApi.csproj

COPY platform-kit/ platform-kit/
COPY core-api/ core-api/
RUN dotnet publish core-api/CoreApi.csproj -c Release -o /app --no-restore

# Self-contained EF migration bundle: applies the migrations without the SDK. Run with --connection "<connection string>".
WORKDIR /src/core-api
RUN dotnet tool restore \
    && dotnet ef migrations bundle --self-contained -r linux-x64 --output /migrate/efbundle --force

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrate
WORKDIR /migrate
COPY --from=build /migrate/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CoreApi.dll"]
