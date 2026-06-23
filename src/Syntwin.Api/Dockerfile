FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json ./
COPY Syntwin.Backend.slnx ./
COPY src/Syntwin.Domain/Syntwin.Domain.csproj src/Syntwin.Domain/
COPY src/Syntwin.Application/Syntwin.Application.csproj src/Syntwin.Application/
COPY src/Syntwin.Infrastructure/Syntwin.Infrastructure.csproj src/Syntwin.Infrastructure/
COPY src/Syntwin.Api/Syntwin.Api.csproj src/Syntwin.Api/

RUN dotnet restore src/Syntwin.Api/Syntwin.Api.csproj

COPY . ./

RUN dotnet publish src/Syntwin.Api/Syntwin.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "Syntwin.Api.dll"]