# Build context: repository root (see docker-compose.yml, profile "full").
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props ChambaIA.sln ./
COPY apps/api/src apps/api/src
COPY apps/worker apps/worker
RUN dotnet restore apps/worker/ChambaIA.Worker/ChambaIA.Worker.csproj
RUN dotnet publish apps/worker/ChambaIA.Worker/ChambaIA.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ChambaIA.Worker.dll"]
