# OpenShort

A self-hosted URL shortener built with .NET 9 and Angular 19.

## Features

- 🔗 Create and manage short links
- 🌐 Multi-domain support
- 🔐 JWT-based authentication with ASP.NET Identity
- 📊 Dashboard with statistics
- 🎨 Modern UI with Angular and PrimeNG
- 🐳 **Single Container Architecture** (Backend + Frontend in one image)
- 💾 **Flexible Storage**: Zero-config SQLite (default) or MySQL support
- 🔄 Collision-resistant slug generation with automatic retry

## Tech Stack

**Backend:**
- .NET 9 (ASP.NET Core Web API)
- Entity Framework Core with MySQL
- ASP.NET Identity for authentication
- FluentValidation
- NUnit for testing

**Frontend:**
- Angular 19 with standalone components
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
   2. Uncomment the `MYSQL_...` environment variables in the `openshort` service.
   3. Uncomment the `mysql` service definition at the bottom.
   4. Run `docker compose up -d`.

3. **Access the application**
   - Application URL: http://localhost:8888

### First Login

Default credentials (created by DbSeeder):
- **Email**: `admin@openshort.local`
- **Password**: `Admin123!`

⚠️ **Change these credentials immediately after first login!**

### Stopping the application

```bash
docker compose down
```

To remove volumes (defaults to deleting SQLite data):
```bash
docker compose down -v
```

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
   docker compose logs -f
   ```

> **Note:** Your `.env` file and database data are preserved during updates. Make sure to keep a backup of your `.env` file in a secure location outside the repository.

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

## Environment Variables

Key variables in `.env`:

Values for `.env` or Docker environment variables:

```env
# Optional: MySQL Configuration (leave empty for SQLite)
MYSQL_HOST=your-mysql-server
MYSQL_PORT=3306
MYSQL_DATABASE=openshort
MYSQL_USER=root
MYSQL_PASSWORD=secure_password

# Security
JWT_SECRET_KEY=your_secure_random_key_at_least_32_chars
ASPNETCORE_ENVIRONMENT=Production
```

### JWT Secret Key

The `JWT_SECRET_KEY` must be changed for production deployments. Generate a secure random key:

**Linux/Mac:**
```bash
openssl rand -base64 32
```

**Windows PowerShell:**
```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

Copy the generated key into your `.env` file.


## Domain Configuration

To use OpenShort with your own domains, follow these steps:

### 1. DNS Setup
Point your domain or subdomain to your server's IP address:
- Create an **A record** pointing to your server's public IP.
- Alternatively, create a **CNAME record** pointing to your server's hostname.

### 2. Reverse Proxy & SSL (Recommended)
It is highly recommended to run OpenShort behind a reverse proxy like **Nginx**, **Traefik**, or **Caddy** with HTTPS enabled.

#### Example Nginx Configuration:
```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://localhost:8888; # Docker container port
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Websocket support (optional, for SignalR/HMR if used)
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
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
