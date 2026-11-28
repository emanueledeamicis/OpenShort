# Stage 1: Build Angular Frontend
# Same major version as frontend/.nvmrc, which the CI reads
FROM node:24 AS frontend-build
WORKDIR /app
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ .
RUN npm run build -- --configuration production

# Stage 2: Build .NET Backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install Nginx and Supervisor
RUN apt-get update && \
    apt-get install -y nginx supervisor && \
    rm -rf /var/lib/apt/lists/*

EXPOSE 8080
EXPOSE 8081

# Copy compiled backend
COPY --from=publish /app/publish .

# Copy compiled frontend to Nginx web root
COPY --from=frontend-build /app/dist/OpenShort.Frontend/browser /var/www/html

# License notices of the third-party components shipped in the image
COPY LICENSE THIRD-PARTY-NOTICES.md /app/licenses/
COPY --from=frontend-build /app/dist/OpenShort.Frontend/3rdpartylicenses.txt /app/licenses/frontend-3rdpartylicenses.txt
# The GPL asks for its text to travel with the MySQL connector: the file comes from the restored package
COPY --from=publish /root/.nuget/packages/mysql.data/*/LICENSE /app/licenses/mysql-connector-net-LICENSE.txt

# Copy configuration files
COPY nginx.conf /etc/nginx/nginx.conf
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Start supervisord directly
ENTRYPOINT ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
