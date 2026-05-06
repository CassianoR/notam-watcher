FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

COPY ["src/NotamWatcher.Api/NotamWatcher.Api.csproj",           "src/NotamWatcher.Api/"]
COPY ["src/NotamWatcher.Domain/NotamWatcher.Domain.csproj",     "src/NotamWatcher.Domain/"]
COPY ["src/NotamWatcher.Parsing/NotamWatcher.Parsing.csproj",   "src/NotamWatcher.Parsing/"]
COPY ["src/NotamWatcher.Infrastructure/NotamWatcher.Infrastructure.csproj", "src/NotamWatcher.Infrastructure/"]
RUN dotnet restore "src/NotamWatcher.Api/NotamWatcher.Api.csproj"

COPY . .
RUN dotnet publish "src/NotamWatcher.Api/NotamWatcher.Api.csproj" \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
VOLUME ["/app/data", "/app/logs"]
ENV Database__Path=/app/data/notams.db
ENTRYPOINT ["dotnet", "NotamWatcher.Api.dll"]
