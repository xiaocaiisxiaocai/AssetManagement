FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/ ./backend/
RUN dotnet restore backend/AssetManagement.sln
RUN dotnet publish backend/src/AssetManagement.Api/AssetManagement.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .
RUN apt-get update \
    && apt-get install -y --no-install-recommends default-mysql-client \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/uploads /app/Backups

EXPOSE 8080
ENTRYPOINT ["dotnet", "AssetManagement.Api.dll"]
