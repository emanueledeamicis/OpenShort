# OpenShort - Self-hosted link shortener

[![License](https://img.shields.io/github/license/emanueledeamicis/OpenShort)](LICENSE)
[![Docker Pulls](https://img.shields.io/docker/pulls/catokx/openshort)](https://hub.docker.com/r/catokx/openshort)
[![GitHub stars](https://img.shields.io/github/stars/emanueledeamicis/OpenShort)](https://github.com/emanueledeamicis/OpenShort/stargazers)

OpenShort is an open source, self-hosted URL shortener for Docker, SQLite, and MySQL.

It is built for people who want a modern Bitly alternative they can run on their own infrastructure, with custom domains, a web dashboard, and easy short link management in a single container.

## Why OpenShort

- Self-hosted link shortener with a modern web dashboard
- Open source and easy to deploy with Docker or Docker Compose
- Works out of the box with SQLite, or with MySQL for external database setups
- Supports custom domains for branded short links
- Supports both permanent (301) and temporary (302) redirects
- Single-container architecture for simple deployments

## Who Is It For?

OpenShort is a good fit for:

- Small businesses that want branded short links on their own domain
- Marketing teams and agencies that need self-hosted campaign links
- Developers and sysadmins looking for a Docker-based Bitly alternative
- Internal teams that want simple short URLs for tools, documentation, onboarding, or shared resources

## Features

- Create and manage short links from a web dashboard
- Custom domain support for branded short URLs
- Permanent (301) and temporary (302) redirects
- JWT-based authentication with ASP.NET Identity
- Modern UI built with Angular and PrimeNG
- Single-container architecture with backend and frontend in one image
- Flexible storage: zero-config SQLite by default or external MySQL
- Collision-resistant slug generation with automatic retry

## Tech Stack

**Backend:**
- .NET 9 (ASP.NET Core Web API)
- Entity Framework Core
- ASP.NET Identity for authentication
- FluentValidation
- NUnit for testing

**Frontend:**
- Angular 21 with standalone components
- PrimeNG UI components
- Tailwind CSS v3
- Reactive Forms

## Quick Start with Docker

### Prerequisites
- Docker and Docker Compose installed

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/emanueledeamicis/OpenShort.git
   cd OpenShort
   ```

2. **Start the application (SQLite default)**
   By default, OpenShort uses an embedded SQLite database. Zero configuration required.
   ```bash
   docker compose up -d
   ```

   **Option: use MySQL**
   To use MySQL instead of SQLite:
   1. Open `docker-compose.yml`.
   2. Add the `MYSQL_...` environment variables to the `openshort` service.
   3. Add a MySQL service definition such as `mysql:8.0`, or point to an external MySQL server.
   4. Run `docker compose up -d`.

3. **Access the application**
   - Dashboard (frontend): `http://<server-ip>:8081`
   - API and redirects (backend): `http://<server-ip>` on port `80`

### First Login

On the first startup, OpenShort creates the default administrator account:
- **Username**: `admin`

When you open the dashboard for the first time, you will be prompted to choose the password for the `admin` account. No default password is shipped with the application.

### Stopping the application

```bash
docker compose down
```

To remove volumes and delete persisted SQLite data:
```bash
docker compose down -v
```

## Installation Options

You can install OpenShort either by pulling the official Docker image or by using Docker Compose.

### Option 1: Docker Run

You can launch OpenShort instantly using the published image `catokx/openshort:latest`.

#### A. Zero-config mode (SQLite)

If you do not have an external database, use the embedded SQLite engine. Mapping `/app/data` is important if you want your links and settings to persist across container restarts.

```bash
docker run -d \
  --name openshort \
  -p 8081:8081 \
  -p 8080:8080 \
  -v openshort-data:/app/data \
  --restart unless-stopped \
  catokx/openshort:latest
```

#### B. MySQL mode

If you already have a MySQL server running, you can connect OpenShort to it by passing the connection parameters. Since the data is stored externally, mapping a local volume is not strictly necessary.

```bash
docker run -d \
  --name openshort \
  -p 8081:8081 \
  -p 8080:8080 \
  -e MYSQL_HOST=your_mysql_host \
  -e MYSQL_PORT=3306 \
  -e MYSQL_DATABASE=openshort \
  -e MYSQL_USER=root \
  -e MYSQL_PASSWORD=your_secure_password \
  --restart unless-stopped \
  catokx/openshort:latest
```

### Option 2: Docker Compose

For a more declarative setup, you can use the provided `docker-compose.yml` file.

1. **Clone the repository**
   ```bash
   git clone https://github.com/emanueledeamicis/OpenShort.git
   cd OpenShort
   ```

2. **Start the services**
   ```bash
   docker compose up -d
   ```

3. **Access the application**
   - Dashboard (frontend): `http://<server-ip>:8081`
   - API and redirects (backend): `http://<server-ip>` on port `80`

## Updating

### Updating the Published Docker Image with Docker Compose

If you are using the published image in `docker-compose.yml`:

1. **Pull the newest image**
   ```bash
   docker compose pull
   ```

2. **Recreate the container**
   ```bash
   docker compose up -d
   ```

3. **Verify the update**
   ```bash
   docker compose ps
   docker compose logs --tail=100
   ```

This keeps your existing Docker volume data, including SQLite data and application settings.

### Updating the Published Docker Image with docker run

If you started OpenShort with `docker run`, update it like this:

1. **Pull the newest image**
   ```bash
   docker pull catokx/openshort:latest
   ```

2. **Stop and remove the old container**
   ```bash
   docker stop openshort
   docker rm openshort
   ```

3. **Start a new container with the same ports, volumes, and environment variables**
   ```bash
   docker run -d \
     --name openshort \
     -p 8081:8081 \
     -p 8080:8080 \
     -v openshort-data:/app/data \
     --restart unless-stopped \
     catokx/openshort:latest
   ```

### Updating from Source

When a new version is available, follow these steps to update your installation:

1. **Pull the latest code**
   ```bash
   cd OpenShort
   git pull
   ```

2. **Rebuild and restart containers**
   ```bash
   docker compose down
   docker compose build --no-cache
   docker compose up -d
   ```

3. **Verify the update**
   ```bash
   docker compose ps
   docker compose logs --tail=100
   ```

> **Note:** The SQLite database and OpenShort settings, including JWT keys, are stored in Docker volumes. Do not delete the volumes with the `-v` flag during an update if you rely on the embedded SQLite database.

## Use Cases

OpenShort is a good fit if you are looking for:

- A self-hosted Bitly alternative
- A Docker-based URL shortener with SQLite or MySQL
- A branded short link service with custom domains
- A lightweight open source link shortener for personal use, internal tools, or small teams

## API Documentation

OpenShort includes an integration-focused API guide for services and automation that use API key authentication.

Read the guide here:

- [Integration API Documentation](docs/api.md)

## Development

### Backend (.NET)

```bash
cd backend/src/OpenShort.Api
dotnet run
```

**Run tests:**
```bash
cd backend/tests/OpenShort.Tests
dotnet test
```

**Entity Framework migrations:**

> **Note:** OpenShort dynamically selects the database provider. By default, running these commands applies SQLite migrations. To run MySQL migrations during development, set the `MYSQL_HOST` environment variable before running them.

```bash
cd backend/src/OpenShort.Api
dotnet ef migrations add MigrationName --project ../OpenShort.Infrastructure
dotnet ef database update --project ../OpenShort.Infrastructure
```

### Frontend (Angular)

```bash
cd frontend
npm install
npm start
```

Frontend dev server: `http://localhost:4200`

**Build for production:**
```bash
npm run build
```

## Project Structure

```text
OpenShort/
|-- backend/
|   |-- src/
|   |   |-- OpenShort.Api/             # Web API controllers
|   |   |-- OpenShort.Core/            # Domain entities
|   |   `-- OpenShort.Infrastructure/  # Data access and services
|   `-- tests/
|       `-- OpenShort.Tests/           # Unit tests
|-- frontend/
|   `-- src/
|       |-- app/
|       |   |-- core/                  # Services, guards, layout
|       |   `-- features/              # Feature modules
|       `-- styles.css                 # Global styles
`-- docker-compose.yml                 # Docker orchestration
```

## Database Configuration

OpenShort is designed with a zero-config approach. By default, if you start the container without providing database parameters, it automatically creates and uses an embedded SQLite database. This is ideal for quick deployments, personal use, demos, and testing.

If you prefer an external database for larger workloads, OpenShort also supports MySQL through environment variables.

## Environment Variables

All environment variables can be configured directly inside the `environment:` section of your `docker-compose.yml` file. Most of them are optional.

```yaml
# Optional: MySQL configuration
- MYSQL_HOST=your-mysql-server
- MYSQL_PORT=3306
- MYSQL_DATABASE=openshort
- MYSQL_USER=root
- MYSQL_PASSWORD=secure_password

# Security
- ASPNETCORE_ENVIRONMENT=Production
- ADMIN_PASSWORD_RESET=temporary_emergency_password
```

> **Note:** If your passwords or keys contain `$` characters, you must escape them as `$$` in `docker-compose.yml` (for example, `Password$$` becomes `Password$$$$`) so Docker does not interpret them as variables.

### Admin Password Recovery

If you lose the admin password in a self-hosted deployment, you can reset it at container startup with the optional `ADMIN_PASSWORD_RESET` environment variable.

1. Add `ADMIN_PASSWORD_RESET=your_new_temporary_password` to the OpenShort container environment.
2. Restart the container.
3. Sign in with username `admin` and the new password.
4. Remove the environment variable from your deployment after the reset is complete.

### JWT Secret Key

The JWT key is auto-generated and securely saved into the database on the first application startup. You do not need to configure it manually for normal installations.

If you have specific needs such as cluster deployments or manual key rotation policies, you can still override the auto-generated key by explicitly passing it as an environment variable:

```yaml
environment:
  - JWT_SECRET_KEY=YourSuperSecretKeyOfAtLeast32CharactersLong
```

If you provide the key this way, OpenShort always prioritizes it over the one stored in SQLite or MySQL.

## Custom Domain Setup

To use OpenShort with your own domains, follow these steps:

### 1. DNS Setup

Point your domain or subdomain to your server IP address:
- Create an **A record** pointing to your server public IP
- Or create a **CNAME record** pointing to your server hostname

### 2. Reverse Proxy and SSL

It is strongly recommended to run OpenShort behind a reverse proxy such as **Nginx**, **Traefik**, or **Caddy** with HTTPS enabled.

#### Example Nginx reverse proxy configuration

```nginx
server {
    listen 80;
    server_name your-short-domain.com;

    # Expose only REST APIs and short link redirects to the public internet.
    # Keep the dashboard private when possible.
    location / {
        proxy_pass http://localhost:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

#### SSL certificates

Use Certbot and Let's Encrypt to obtain and manage certificates:

```bash
sudo certbot --nginx -d your-domain.com
```

### 3. Adding Domains to OpenShort

Once your domain is pointing to the server, sign in to the OpenShort dashboard and add the domain in the **Domains** section to start using it for your short links.

## License

MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome. Please open an issue or submit a pull request.
