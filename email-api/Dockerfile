# Build with the repository root as the context (it references ../platform-kit):
#   docker build -f email-api/Dockerfile --target api -t email-api .                (the API)
#   docker build -f email-api/Dockerfile --target migrate -t email-api-migrate .    (one-shot database migration job)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the layer is cached until a project file changes.
COPY platform-kit/PlatformKit.csproj platform-kit/
COPY email-api/EmailApi.csproj email-api/dotnet-tools.json email-api/
RUN dotnet restore email-api/EmailApi.csproj

COPY platform-kit/ platform-kit/
COPY email-api/ email-api/
RUN dotnet publish email-api/EmailApi.csproj -c Release -o /app --no-restore

# Self-contained EF migration bundle: applies the migrations without the SDK. Run with --connection "<connection string>".
WORKDIR /src/email-api
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
ENTRYPOINT ["dotnet", "EmailApi.dll"]
