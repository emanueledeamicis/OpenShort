# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install EF Core tools for migrations
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy csproj files and restore dependencies
COPY ["src/OpenShort.Api/OpenShort.Api.csproj", "OpenShort.Api/"]
COPY ["src/OpenShort.Core/OpenShort.Core.csproj", "OpenShort.Core/"]
COPY ["src/OpenShort.Infrastructure/OpenShort.Infrastructure.csproj", "OpenShort.Infrastructure/"]
RUN dotnet restore "OpenShort.Api/OpenShort.Api.csproj"

# Copy everything else and build
COPY src/ .
WORKDIR "/src/OpenShort.Api"
RUN dotnet build "OpenShort.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "OpenShort.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Migrations (uses build stage with SDK and EF tools)
FROM build AS migrations
WORKDIR /src
COPY run-migrations.sh /src/run-migrations.sh
RUN chmod +x /src/run-migrations.sh
ENTRYPOINT ["/src/run-migrations.sh"]

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenShort.Api.dll"]
