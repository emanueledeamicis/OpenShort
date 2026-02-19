# Stage 1: Build Angular Frontend
FROM node:20 AS frontend-build
WORKDIR /app
COPY ../frontend/package*.json ./
RUN npm ci
COPY ../frontend/ .
RUN npm run build -- --configuration production

# Stage 2: Build .NET Backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["backend/src/OpenShort.Api/OpenShort.Api.csproj", "OpenShort.Api/"]
COPY ["backend/src/OpenShort.Core/OpenShort.Core.csproj", "OpenShort.Core/"]
COPY ["backend/src/OpenShort.Infrastructure/OpenShort.Infrastructure.csproj", "OpenShort.Infrastructure/"]
RUN dotnet restore "OpenShort.Api/OpenShort.Api.csproj"

# Copy everything else and build
COPY backend/src/ .
WORKDIR "/src/OpenShort.Api"
RUN dotnet build "OpenShort.Api.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "OpenShort.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Final Image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

# Copy compiled backend
COPY --from=publish /app/publish .

# Copy compiled frontend to wwwroot
COPY --from=frontend-build /app/dist/openshort/browser ./wwwroot

ENTRYPOINT ["dotnet", "OpenShort.Api.dll"]
