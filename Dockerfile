# ==============================================================================
# NPTEL Management System - Production Dockerfile (Multi-Stage Build)
# ==============================================================================

# Stage 1: Build and Publish Application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching on restore
COPY ["Core/NPTELManagement.Core.csproj", "Core/"]
COPY ["Infrastructure/NPTELManagement.Infrastructure.csproj", "Infrastructure/"]
COPY ["Api/NPTELManagement.Api.csproj", "Api/"]

RUN dotnet restore "Api/NPTELManagement.Api.csproj"

# Copy application source code
COPY Core/ Core/
COPY Infrastructure/ Infrastructure/
COPY Api/ Api/

WORKDIR "/src/Api"
RUN dotnet publish "NPTELManagement.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Configure default container port & environment
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run as built-in secure non-root user (available in .NET 8 images)
USER $APP_UID

# Copy compiled application binaries
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NPTELManagement.Api.dll"]
