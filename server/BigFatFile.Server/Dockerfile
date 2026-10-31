# The Origin server image. To build it, from the repository root run:
#   docker build -f server/BigFatFile.Server/Dockerfile .
# The SDK stage runs on the build machine and cross-compiles for the target architecture.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
ARG VERSION=0.1.0-dev
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY sdk/BigFatFile.Contracts/BigFatFile.Contracts.csproj sdk/BigFatFile.Contracts/
COPY sdk/BigFatFile.Migrator/BigFatFile.Migrator.csproj sdk/BigFatFile.Migrator/
COPY server/BigFatFile.Abstractions/BigFatFile.Abstractions.csproj server/BigFatFile.Abstractions/
COPY server/BigFatFile.Server/BigFatFile.Server.csproj server/BigFatFile.Server/
RUN dotnet restore server/BigFatFile.Server/BigFatFile.Server.csproj --arch $TARGETARCH

COPY sdk/BigFatFile.Contracts/ sdk/BigFatFile.Contracts/
COPY sdk/BigFatFile.Migrator/ sdk/BigFatFile.Migrator/
COPY server/BigFatFile.Abstractions/ server/BigFatFile.Abstractions/
COPY server/BigFatFile.Server/ server/BigFatFile.Server/
RUN dotnet publish server/BigFatFile.Server/BigFatFile.Server.csproj \
    --arch $TARGETARCH --configuration Release --no-restore --output /app \
    -p:Version=$VERSION -p:OpenApiGenerateDocuments=false

# Chiseled: no shell, no package manager, runs as a non-root user. The "extra" variant
# carries ICU, which name keys need for Unicode normalisation.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
ARG VERSION=0.1.0-dev
LABEL org.opencontainers.image.title="BigFatFile" \
      org.opencontainers.image.source="https://github.com/vpalos/BigFatFile" \
      org.opencontainers.image.version="$VERSION"
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 CMD ["/app/BigFatFile.Server", "health"]
ENTRYPOINT ["/app/BigFatFile.Server"]
