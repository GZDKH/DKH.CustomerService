# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 5010

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy nuget.config first for package restoration
COPY ["nuget.config", "."]

# Copy project files for restore
COPY ["DKH.CustomerService.Api/DKH.CustomerService.Api.csproj", "DKH.CustomerService.Api/"]
COPY ["DKH.CustomerService.Application/DKH.CustomerService.Application.csproj", "DKH.CustomerService.Application/"]
COPY ["DKH.CustomerService.Contracts/DKH.CustomerService.Contracts.csproj", "DKH.CustomerService.Contracts/"]
COPY ["DKH.CustomerService.Domain/DKH.CustomerService.Domain.csproj", "DKH.CustomerService.Domain/"]
COPY ["DKH.CustomerService.Infrastructure/DKH.CustomerService.Infrastructure.csproj", "DKH.CustomerService.Infrastructure/"]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["project.props", "."]

# Restore with GitHub NuGet secrets
RUN --mount=type=secret,id=github_nuget_username \
    --mount=type=secret,id=github_nuget_token \
    GITHUB_NUGET_USERNAME=$(cat /run/secrets/github_nuget_username) \
    GITHUB_NUGET_TOKEN=$(cat /run/secrets/github_nuget_token) \
    dotnet restore "DKH.CustomerService.Api/DKH.CustomerService.Api.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/DKH.CustomerService.Api"
RUN dotnet build "DKH.CustomerService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DKH.CustomerService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DKH.CustomerService.Api.dll"]
