# docker build -f .\Dockerfile ../
# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER $APP_UID
WORKDIR /app

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SosuBot.Render/SosuBot.Render.csproj", "SosuBot.Render/"]
COPY ["SosuBot.Database/SosuBot.Database.csproj", "SosuBot.Database/"]
RUN dotnet restore "./SosuBot.Render/SosuBot.Render.csproj"
COPY . .
WORKDIR "/src/SosuBot.Render"
RUN dotnet build "./SosuBot.Render.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SosuBot.Render.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SosuBot.Render.dll"]