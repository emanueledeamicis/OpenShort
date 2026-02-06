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

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

# Install dotnet-ef for runtime migrations
RUN apt-get update && apt-get install -y curl && \
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0 --install-dir /usr/share/dotnet && \
    ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet && \
    dotnet tool install --global dotnet-ef --version 9.* && \
    apt-get clean && rm -rf /var/lib/apt/lists/*
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy published app and source for migrations
COPY --from=publish /app/publish .
COPY --from=build /src /src

# Copy migration script
COPY run-migrations.sh /app/run-migrations.sh
RUN chmod +x /app/run-migrations.sh

ENTRYPOINT ["dotnet", "OpenShort.Api.dll"]
