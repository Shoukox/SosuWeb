# docker build -f .\Dockerfile ../
# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER $APP_UID
WORKDIR /app

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SosuWeb.Render/SosuWeb.Render.csproj", "SosuWeb.Render/"]
COPY ["SosuWeb.Database/SosuWeb.Database.csproj", "SosuWeb.Database/"]
RUN dotnet restore "./SosuWeb.Render/SosuWeb.Render.csproj"
COPY . .
WORKDIR "/src/SosuWeb.Render"
RUN dotnet build "./SosuWeb.Render.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SosuWeb.Render.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SosuWeb.Render.dll"]