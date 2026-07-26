FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json ./
COPY Syntwin.Backend.slnx ./
COPY src/Syntwin.Domain/Syntwin.Domain.csproj src/Syntwin.Domain/
COPY src/Syntwin.Application/Syntwin.Application.csproj src/Syntwin.Application/
COPY src/Syntwin.Infrastructure/Syntwin.Infrastructure.csproj src/Syntwin.Infrastructure/
COPY src/Syntwin.DbMigrator/Syntwin.DbMigrator.csproj src/Syntwin.DbMigrator/

RUN dotnet restore src/Syntwin.DbMigrator/Syntwin.DbMigrator.csproj

COPY . ./

RUN dotnet publish src/Syntwin.DbMigrator/Syntwin.DbMigrator.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Syntwin.DbMigrator.dll"]
