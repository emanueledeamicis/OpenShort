# OpenShort - Self-hosted link shortener

[![License](https://img.shields.io/github/license/emanueledeamicis/OpenShort)](LICENSE)
[![Docker Pulls](https://img.shields.io/docker/pulls/catokx/openshort)](https://hub.docker.com/r/catokx/openshort)
[![GitHub stars](https://img.shields.io/github/stars/emanueledeamicis/OpenShort)](https://github.com/emanueledeamicis/OpenShort/stargazers)

OpenShort is an open source, self-hosted URL shortener for Docker, SQLite, and MySQL.

It is built for people who want a modern Bitly alternative they can run on their own infrastructure, with custom domains, a web dashboard, and easy short link management in a single container.

Project website: [openshort.tech](https://openshort.tech)

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
- .NET 10 (ASP.NET Core Web API)
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

You can delete the `admin` account: create another administrator in the dashboard **Users** section, sign in with it and delete `admin` from there. It is not created again at the next start, unless you ask for it with the [admin password recovery](#admin-password-recovery).

After signing in, open the dashboard **Security** section to generate or manage the API key used by external integrations. Programmatic requests to the integration API must send that key in the `X-Api-Key` header.

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

Important:

- Integration API requests require an API key in the `X-Api-Key` header
- The API key is generated and managed from the OpenShort dashboard in the **Security** section
- Dashboard login uses JWT authentication, but external integrations should use the API key instead

Read the guide here:

- [API Documentation](https://openshort.tech/docs/#api)
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

If you prefer an external database for larger workloads, OpenShort also supports MySQL through environment variables. MariaDB is not supported.

## Environment Variables

All environment variables can be configured directly inside the `environment:` section of your `docker-compose.yml` file. Most of them are optional.

```yaml
# Optional: MySQL configuration
- MYSQL_HOST=your-mysql-server
- MYSQL_PORT=3306
- MYSQL_DATABASE=openshort
- MYSQL_USER=root
- MYSQL_PASSWORD=secure_password
# Optional: None (default), Preferred, Required, VerifyCA or VerifyFull
# - MYSQL_SSL_MODE=Required

# Security
- ASPNETCORE_ENVIRONMENT=Production

# Recovery only: resets the admin password at every start, remove it after use (see "Admin Password Recovery")
# - ADMIN_PASSWORD_RESET=<new-password>

# Optional: reverse proxies allowed to report the client IP address (see "Trusted proxies")
- TRUSTED_PROXIES=172.30.0.1
```

> **Note:** If your passwords or keys contain `$` characters, you must escape them as `$$` in `docker-compose.yml` (for example, `Password$$` becomes `Password$$$$`) so Docker does not interpret them as variables.

> **Note:** The `MYSQL_*` values are used exactly as written, so a password can contain any character. Do not put quotes around them: in `- MYSQL_PASSWORD="secret"` the quotes become part of the password. To set other connection options, pass a full connection string in `ConnectionStrings__DefaultConnection` instead of the `MYSQL_*` variables.

> **Note:** By default the connection to MySQL is not encrypted. If the database is reached over a network you do not control, set `MYSQL_SSL_MODE` to `VerifyCA` or `VerifyFull`: they encrypt the connection and check the server certificate. `Required` encrypts without checking the certificate; `Preferred` encrypts only when the server offers TLS. TLS does not work with MySQL 5.7.

### Admin Password Recovery

If you lose the admin password in a self-hosted deployment, you can reset it at container startup with the optional `ADMIN_PASSWORD_RESET` environment variable.

1. Add `ADMIN_PASSWORD_RESET=<new-password>` to the OpenShort container environment, with a password of your own.
2. Restart the container.
3. Sign in with username `admin` and the new password.
4. Remove the environment variable from your deployment after the reset is complete. As long as it is set, the admin password is reset to that value at every start, even if you changed it from the dashboard in the meantime.

The new password must follow the same rules as any other password: at least 8 characters, with an uppercase letter, a lowercase letter, a digit and a symbol, and no spaces at the beginning or at the end. If it does not, the current password is left unchanged and the container logs report the error (`ADMIN_PASSWORD_RESET was not applied ...`) with the reason.

If the `admin` account was deleted, the recovery creates it again with the new password, at every start for as long as the variable is set.

The reset does not sign out sessions that are already open: a token issued before the reset stays valid until it expires, up to 7 days.

### JWT Secret Key

The JWT key is auto-generated and securely saved into the database on the first application startup. You do not need to configure it manually for normal installations.

If you have specific needs such as cluster deployments or manual key rotation policies, you can still override the auto-generated key by explicitly passing it as an environment variable:

```yaml
environment:
  - JWT_SECRET_KEY=YourSuperSecretKeyOfAtLeast32CharactersLong
```

If you provide the key this way, OpenShort always prioritizes it over the one stored in SQLite or MySQL.

**Rotating the key.** Start OpenShort once with `JWT_SECRET_KEY` set to a new random value of at least 32 characters. Sessions opened before are no longer accepted, so everyone has to sign in again. The new key is saved in the database, so you can remove the variable afterwards.

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

#### Trusted proxies

OpenShort uses the client IP address to limit login attempts. A reverse proxy reports that address in the `X-Forwarded-For` header, and OpenShort accepts the header only from proxies it trusts. Otherwise any client could pretend to be someone else.

Out of the box only the Nginx bundled in the container is trusted. When you put your own reverse proxy in front of OpenShort, declare it with the `TRUSTED_PROXIES` environment variable.

Without this setting OpenShort ignores every `X-Forwarded-*` header sent by your proxy:

- It sees every visitor with the IP address of your proxy. Login attempts are then limited for all visitors together: repeated wrong passwords from anyone slow down everyone, including you.
- If your proxy replaces the `Host` header and sends the original domain in `X-Forwarded-Host`, short links stop resolving and show the "link not found" page. The Nginx example above is not affected, because it forwards `Host`.

**Which address to declare.** Declare the address OpenShort sees your proxy connecting from. When forwarded headers arrive from an address that is not trusted, OpenShort writes it in a warning in the container logs (`Ignored forwarded headers received from ...`): start without the setting, send one request through your proxy and read the address there.

- For a proxy running on the Docker host it is usually the gateway of the Docker network.
- For a proxy running in another container it is the address of that container on the shared network.
- If there are more proxies in a row, for example a CDN in front of your proxy, declare each of them. Otherwise visitors are seen with the address of the first undeclared one.

**Example: proxy running on the Docker host.** Give the Docker network a fixed subnet, so the gateway address does not change, and publish the ports only on `127.0.0.1`:

```yaml
services:
  openshort:
    ports:
      - "127.0.0.1:8081:8081"
      - "127.0.0.1:8080:8080"
    environment:
      - TRUSTED_PROXIES=172.30.0.1
    networks:
      - openshort

networks:
  openshort:
    ipam:
      config:
        - subnet: 172.30.0.0/24
          gateway: 172.30.0.1
```

Your proxy then forwards to `http://127.0.0.1:8080` (API and short links) and `http://127.0.0.1:8081` (dashboard).

**Rules for the value.**

- It is a comma-separated list of IP addresses or CIDR networks, for example `172.30.0.1,10.0.0.0/24`. Host names are not supported, and an invalid value stops the application at startup.
- Every declared address can choose the IP address it is seen with. Declare single addresses of proxies you control, and avoid wide networks such as `172.16.0.0/12`: they also cover other containers and machines.
- Nobody else must be able to connect from a declared address. When you declare the Docker gateway, publishing the ports only on `127.0.0.1` (or closing them with a firewall) is required: with Docker Desktop and rootless Docker every connection to a published port comes from the gateway, and on Linux some do as well.
- Your proxy must add the client address to `X-Forwarded-For`, as in the Nginx example above. Caddy and Traefik do it by default.

#### SSL certificates

Use Certbot and Let's Encrypt to obtain and manage certificates:

```bash
sudo certbot --nginx -d your-domain.com
```

### 3. Adding Domains to OpenShort

Once your domain is pointing to the server, sign in to the OpenShort dashboard and add the domain in the **Domains** section to start using it for your short links.

## License

MIT License - see the [LICENSE](LICENSE) file for details.

## Author

OpenShort is created by [Emanuele De Amicis](https://emanueledeamicis.com).

## Contributing

Contributions are welcome. Please open an issue or submit a pull request.
