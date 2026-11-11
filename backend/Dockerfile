# syntax=docker/dockerfile:1
# Build context: backend/. Produces the API image (framework-dependent, non-root, port 8080).

# The SDK 10.0 image satisfies global.json (10.0.300, rollForward latestMinor).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS projects
WORKDIR /src
COPY src ./src
# Keep only the files restore needs, so the restore layer below is reused until a project or props file changes.
# Discovering project files (rather than listing them) means new projects need no Dockerfile edit.
RUN find src -type f ! -name '*.csproj' ! -name 'Directory.*.props' -delete

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY --from=projects /src/src ./src
RUN dotnet restore src/Api/Api.csproj
COPY src ./src
RUN dotnet publish src/Api/Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "EmployeePayroll.Api.dll"]
