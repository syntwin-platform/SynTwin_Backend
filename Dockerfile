FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Syntwin.Api/Syntwin.Api.csproj", "src/Syntwin.Api/"]
COPY ["src/Syntwin.Application/Syntwin.Application.csproj", "src/Syntwin.Application/"]
COPY ["src/Syntwin.Domain/Syntwin.Domain.csproj", "src/Syntwin.Domain/"]
COPY ["src/Syntwin.Infrastructure/Syntwin.Infrastructure.csproj", "src/Syntwin.Infrastructure/"]
RUN dotnet restore "src/Syntwin.Api/Syntwin.Api.csproj"
COPY . .
WORKDIR "/src/src/Syntwin.Api"
RUN dotnet build "Syntwin.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Syntwin.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Syntwin.Api.dll"]
