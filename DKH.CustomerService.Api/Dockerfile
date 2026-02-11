# syntax=docker/dockerfile:1
# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 5010

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  DOTNET_CLI_TELEMETRY_OPTOUT=1

# Fix for gRPC protoc segfault on arm64: install system protobuf-compiler
# On amd64, bundled protoc works fine, so skip installation
RUN if [ "$(uname -m)" = "aarch64" ] || [ "$(uname -m)" = "arm64" ]; then \
  echo "==> Installing system protoc for arm64 to fix Grpc.Tools segfault"; \
  apt-get update && \
  apt-get install -y --no-install-recommends protobuf-compiler && \
  rm -rf /var/lib/apt/lists/* && \
  protoc --version; \
  else \
  echo "==> Using bundled Grpc.Tools protoc on $(uname -m)"; \
  fi

WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["nuget.config", "."]

COPY ["project.props", "."]

# Inject GitHub NuGet credentials via dotnet CLI (secure, no layer caching)
RUN --mount=type=secret,id=github_nuget_username \
    --mount=type=secret,id=github_nuget_token \
    if [ ! -s /run/secrets/github_nuget_username ] || [ ! -s /run/secrets/github_nuget_token ]; then \
    echo "Missing GitHub NuGet secrets (GITHUB_NUGET_USERNAME/TOKEN)" && exit 1; \
    fi && \
    dotnet nuget update source github-dotnet-gzdkh \
    --username "$(cat /run/secrets/github_nuget_username)" \
    --password "$(cat /run/secrets/github_nuget_token)" \
    --store-password-in-clear-text \
    --configfile nuget.config
COPY ["DKH.CustomerService.Api/DKH.CustomerService.Api.csproj", "DKH.CustomerService.Api/"]
COPY ["DKH.CustomerService.Application/DKH.CustomerService.Application.csproj", "DKH.CustomerService.Application/"]
COPY ["DKH.CustomerService.Contracts/DKH.CustomerService.Contracts.csproj", "DKH.CustomerService.Contracts/"]
COPY ["DKH.CustomerService.Domain/DKH.CustomerService.Domain.csproj", "DKH.CustomerService.Domain/"]
COPY ["DKH.CustomerService.Infrastructure/DKH.CustomerService.Infrastructure.csproj", "DKH.CustomerService.Infrastructure/"]
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    RUNTIME_ID=$(if [ "$(uname -m)" = "aarch64" ] || [ "$(uname -m)" = "arm64" ]; then echo "linux-arm64"; else echo "linux-x64"; fi) && \
    dotnet restore "DKH.CustomerService.Api/DKH.CustomerService.Api.csproj" -r $RUNTIME_ID /p:PublishReadyToRun=true
COPY . .

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/DKH.CustomerService.Api"
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    RUNTIME_ID=$(if [ "$(uname -m)" = "aarch64" ] || [ "$(uname -m)" = "arm64" ]; then echo "linux-arm64"; else echo "linux-x64"; fi) && \
    dotnet restore "./DKH.CustomerService.Api.csproj" -r $RUNTIME_ID /p:PublishReadyToRun=true && \
    if [ "$(uname -m)" = "aarch64" ] || [ "$(uname -m)" = "arm64" ]; then \
    Protobuf_ProtocPath=/usr/bin/protoc \
    dotnet publish "./DKH.CustomerService.Api.csproj" --no-restore -c $BUILD_CONFIGURATION -o /app/publish -r $RUNTIME_ID /p:UseAppHost=false /p:PublishReadyToRun=true; \
    else \
    dotnet publish "./DKH.CustomerService.Api.csproj" --no-restore -c $BUILD_CONFIGURATION -o /app/publish -r $RUNTIME_ID /p:UseAppHost=false /p:PublishReadyToRun=true; \
    fi

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DKH.CustomerService.Api.dll"]
