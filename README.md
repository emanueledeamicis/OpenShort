# OpenShort

A self-hosted URL shortener built with .NET 9 and Angular 21.

## Features

- 🔗 Create and manage short links
- 🌐 Multi-domain support
- 🔐 JWT-based authentication with ASP.NET Identity
- 🎨 Modern UI with Angular and PrimeNG
- 🐳 **Single Container Architecture** (Backend + Frontend in one image)
- 💾 **Flexible Storage**: Zero-config SQLite (default) or external MySQL support
- 🔄 Collision-resistant slug generation with automatic retry

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
- Docker & Docker Compose installed

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/emanueledeamicis/OpenShort.git
   cd OpenShort
   ```

2. **Start the application (SQLite Default)**
   By default, OpenShort uses an embedded SQLite database. Zero configuration required!
   ```bash
   docker compose up -d
   ```

   **Option: Use MySQL**
   To use MySQL instead of SQLite:
   1. Open `docker-compose.yml`.
   2. Add the `MYSQL_...` environment variables to the `openshort` service (see "Environment Variables" section below).
   3. Add a MySQL service definition (standard `mysql:8.0` image) or use an external database.
   4. Run `docker compose up -d`.

3. **Access the application**
   - Dashboard (Frontend): http://{{server-ip}}:8081
   - API & Redirects (Backend): http://{{server-ip}} (default on port 80)

### First Login

On the first startup, OpenShort creates the default administrator account:
- **Username**: `admin`

When you open the dashboard for the first time, you will be prompted to choose the admin password and confirm it. No default password is shipped with the application.

### Stopping the application

```bash
docker compose down
```

To remove volumes (⚠️⚠️⚠️ defaults to deleting SQLite data):
```bash
docker compose down -v
```

## Installation Options

You can install OpenShort either by pulling the official Docker image or by using Docker Compose.

### Option 1: Docker Run (Quickest)

You can launch OpenShort instantly using the published image (`catokx/openshort:latest`) directly from the terminal. 

#### A. Zero-Config Mode (SQLite)
If you don't have an external database, simply use the embedded SQLite engine. **Note:** Mapping the `/app/data` volume is crucial to persist your links and settings across container restarts!

```bash
docker run -d \
  --name openshort \
  -p 8081:8081 \
  -p 8080:8080 \
  -v openshort-data:/app/data \
  --restart unless-stopped \
  catokx/openshort:latest
```

#### B. MySQL Mode
If you already have a MySQL server running, you can connect OpenShort to it by passing the connection parameters. Since all data is stored externally, mapping a local volume is not strictly necessary.

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
   - Dashboard (Frontend): http://{{server-ip}}:8081
   - API & Redirects (Backend): http://{{server-ip}} (default on port 80)

## Updating

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
> **Note:** The SQLite database and OpenShort settings (including JWT keys) are pre-configured out-of-the-box and stored in Docker Volumes! Make sure you don't delete the volumes using the `-v` flag during the update process if you rely on the embedded SQLite database.

> **Future:** When pre-built images are published to a container registry, the update process will be simplified to `docker compose pull && docker compose up -d`.


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
> **Note:** OpenShort dynamically selects the database provider. By default, running these commands applies SQLite migrations. To run MySQL migrations during development, you must set the `MYSQL_HOST` environment variable before running the command.

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

Frontend dev server: http://localhost:4200

**Build for production:**
```bash
npm run build
```

## Project Structure

```
OpenShort/
├── backend/
│   ├── src/
│   │   ├── OpenShort.Api/          # Web API controllers
│   │   ├── OpenShort.Core/         # Domain entities
│   │   └── OpenShort.Infrastructure/ # Data access, services
│   └── tests/
│       └── OpenShort.Tests/        # Unit tests
├── frontend/
│   └── src/
│       ├── app/
│       │   ├── core/               # Services, guards, layout
│       │   └── features/           # Feature modules
│       └── styles.css              # Global styles
└── docker-compose.yml              # Docker orchestration
```

## Database Configuration

OpenShort is designed with a **Zero-Config** approach. By default, if you spin up the container without providing any database parameters, it will automatically create and use an **embedded SQLite database**. This is perfect for quick deployments, personal use, or testing!

If you prefer a more robust external database for heavy workloads, OpenShort fully supports **MySQL**. You just need to provide the MySQL connection parameters via Environment Variables.

## Environment Variables

All Environment Variables can be configured directly inside the `environment:` section of your `docker-compose.yml` file. Most of them are entirely optional!

```yaml
# Optional: MySQL Configuration (leave empty for SQLite default logic)
- MYSQL_HOST=your-mysql-server
- MYSQL_PORT=3306
- MYSQL_DATABASE=openshort
- MYSQL_USER=root
- MYSQL_PASSWORD=secure_password

# Security
- ASPNETCORE_ENVIRONMENT=Production
- ADMIN_PASSWORD_RESET=temporary_emergency_password

> **Note:** If your passwords or keys contain `$` characters, you must escape them as `$$` in `docker-compose.yml` (e.g. `Password$$` becomes `Password$$$$`) to prevent Docker from interpreting them as variables.
```

### Admin Password Recovery

If you lose the admin password in a self-hosted deployment, you can reset it at container startup with the optional `ADMIN_PASSWORD_RESET` environment variable.

1. Add `ADMIN_PASSWORD_RESET=your_new_temporary_password` to the OpenShort container environment.
2. Restart the container.
3. Sign in with username `admin` and the new password.
4. Remove the environment variable from your deployment after the reset is complete.

### JWT Secret Key

> 💡 **New in OpenShort v1.1+**: The JWT Key (`JWT_SECRET_KEY`) is now **auto-generated and securely saved into the database** on the very first application startup. You no longer need to configure it manually, ensuring a true *Zero-Config* and secure installation out of the box!

If you have specific needs (e.g., cluster deployments or manual key rotation policies), you can still override the auto-generated key by explicitly passing it as an environment variable or argument:

1. Inside your `docker-compose.yml` or via the `docker run` command, simply add the parameter under the `environment:` section:
   ```yaml
   environment:
     - JWT_SECRET_KEY=YourSuperSecretKeyOfAtLeast32CharactersLong
   ```

If you provide the key this way, OpenShort will **always prioritize it** over the one stored in the local SQLite or MySQL database.


To use OpenShort with your own domains, follow these steps:

### 1. DNS Setup
Point your domain or subdomain to your server's IP address:
- Create an **A record** pointing to your server's public IP.
- Alternatively, create a **CNAME record** pointing to your server's hostname.

### 2. Reverse Proxy & SSL (Recommended)
It is highly recommended to run OpenShort behind a reverse proxy like **Nginx**, **Traefik**, or **Caddy** with HTTPS enabled.

#### Example Nginx Reverse Proxy Configuration:
```nginx
server {
    listen 80;
    server_name your-short-domain.com;

    # Expose only REST APIs and Short Link Redirects to the public internet
    # The Dashboard is omitted for security and accessible only via IP:8081 locally or via VPN
    location / {
        proxy_pass http://localhost:80; # Docker mapped backend port
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

#### SSL Certificates
Use **Certbot (Let's Encrypt)** to easily obtain and manage SSL certificates:
```bash
sudo certbot --nginx -d your-domain.com
```

### 3. Adding Domains to OpenShort
Once your domain is pointing to the server, log in to the OpenShort dashboard and add the domain in the **Domains** section to start using it for your short links.

## License

MIT License - see LICENSE file for details

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

