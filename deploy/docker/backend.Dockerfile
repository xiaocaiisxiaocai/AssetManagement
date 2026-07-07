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
RUN mkdir -p /app/uploads /app/backups

EXPOSE 8080
ENTRYPOINT ["dotnet", "AssetManagement.Api.dll"]
