# OpenShort

A self-hosted URL shortener built with .NET 9 and Angular 19.

## Features

- 🔗 Create and manage short links
- 🌐 Multi-domain support
- 🔐 Cookie-based authentication with ASP.NET Identity
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
   git clone https://github.com/yourusername/OpenShort.git
   cd OpenShort
   ```

2. **Configure environment variables**
   ```bash
   cp .env.example .env
   # Edit .env and set secure passwords
   ```

3. **Start the application**
   ```bash
   docker-compose up -d
   ```

4. **Access the application**
   - Frontend: http://localhost
   - Backend API: http://localhost:5000

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
```

## License

MIT License - see LICENSE file for details

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.
