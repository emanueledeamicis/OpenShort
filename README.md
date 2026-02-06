# OpenShort

A self-hosted URL shortener built with .NET 9 and Angular 19.

## Features

- 🔗 Create and manage short links
- 🌐 Multi-domain support
- 🔐 JWT-based authentication with ASP.NET Identity
- 📊 Dashboard with statistics
- 🎨 Modern UI with Angular and PrimeNG
- 🐳 Docker deployment ready

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

2. **Configure environment variables**
   ```bash
   cp .env.example .env
   # Edit .env and set secure passwords for mysql and jwt encryption
   ```

3. **Start the application**
   ```bash
   docker compose up -d
   ```

4. **Access the application**
   - Frontend: http://localhost:83
   - Backend API: http://localhost:6000

### First Login

Default credentials (created by DbSeeder):
- **Email**: `admin@openshort.local`
- **Password**: `Admin123!`

⚠️ **Change these credentials immediately after first login!**

### Stopping the application

```bash
docker-compose down
```

To remove volumes (⚠️ deletes all data):
```bash
docker-compose down -v
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

```env
MYSQL_ROOT_PASSWORD=your_secure_password
MYSQL_DATABASE=openshort
ASPNETCORE_ENVIRONMENT=Production
JWT_SECRET_KEY=your_secure_random_key
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
        proxy_pass http://localhost:83; # Frontend port
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /api {
        proxy_pass http://localhost:6000; # Backend API port
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
