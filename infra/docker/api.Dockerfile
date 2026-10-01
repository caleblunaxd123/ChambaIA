# Build context: repository root (see docker-compose.yml, profile "full").
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props ChambaIA.sln ./
COPY apps/api/src apps/api/src
COPY apps/worker apps/worker
COPY apps/api/tests/ChambaIA.Tests/ChambaIA.Tests.csproj apps/api/tests/ChambaIA.Tests/
RUN dotnet restore apps/api/src/ChambaIA.Api/ChambaIA.Api.csproj
RUN dotnet publish apps/api/src/ChambaIA.Api/ChambaIA.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
# Runs as the non-root "app" user shipped with the base image.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChambaIA.Api.dll"]
