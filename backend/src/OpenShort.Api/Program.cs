using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OpenShort.Infrastructure.Data;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;
using OpenShort.Core;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Web Dasbhoard Configuration
var contentRoot = builder.Environment.ContentRootPath;
var webRoot = Path.Combine(contentRoot, "wwwroot");

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseProvider = builder.Configuration["DatabaseProvider"];

// Check for individual MYSQL_ environment variables to support container configuration
var mysqlHost = Environment.GetEnvironmentVariable("MYSQL_HOST");
if (!string.IsNullOrEmpty(mysqlHost))
{
    var mysqlPort = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306";
    var mysqlDatabase = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "openshort";
    var mysqlUser = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "root";
    var mysqlPassword = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "";
    
    connectionString = $"Server={mysqlHost};Port={mysqlPort};Database={mysqlDatabase};User={mysqlUser};Password={mysqlPassword};SslMode=None;";
    databaseProvider = "MySql"; // Force provider if env vars are present
}

// Determine provider based on configuration or connection string format
bool useMySql = string.Equals(databaseProvider, "MySql", StringComparison.OrdinalIgnoreCase) || 
                (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase));

if (useMySql)
{
    Console.WriteLine($"Using MySQL Database Provider. Connection String: {connectionString}");
    builder.Services.AddDbContext<AppDbContext, MySqlDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
}
else
{
    // Ensure data directory exists for SQLite
    if (!Directory.Exists("data"))
    {
        Directory.CreateDirectory("data");
    }

    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = "Data Source=data/openshort.db";
    }
    
    Console.WriteLine($"Using SQLite Database Provider. Connection String: {connectionString}");
    builder.Services.AddDbContext<AppDbContext, SqliteDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Configure SlugSettings
builder.Services.Configure<SlugSettings>(builder.Configuration.GetSection("SlugSettings"));

builder.Services.AddScoped<ISlugGenerator, SlugGenerator>();
builder.Services.AddScoped<IDomainService, DomainService>();
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>(); // Register Token Service
builder.Services.AddScoped<IApiKeyService, ApiKeyService>(); // Register ApiKey Service


// --- JWT AUTHENTICATION SETUP ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForDevelopmentOnly_ChangeInProduction_AtLeast32CharsLong")),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ClockSkew = TimeSpan.Zero
    };
})
.AddScheme<OpenShort.Api.Auth.ApiKeyAuthOptions, OpenShort.Api.Auth.ApiKeyAuthHandler>(
    OpenShort.Api.Auth.ApiKeyAuthOptions.DefaultScheme, 
    options => { } // No static config needed, uses IApiKeyService
);

builder.Services.AddAuthorization(options =>
{
    // Ensure that the default policy enforces authentication
    // but allows EITHER Identity JWT OR ApiKey
    var defaultAuthorizationPolicyBuilder = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, 
        OpenShort.Api.Auth.ApiKeyAuthOptions.DefaultScheme);
    
    defaultAuthorizationPolicyBuilder = defaultAuthorizationPolicyBuilder.RequireAuthenticatedUser();
    options.DefaultPolicy = defaultAuthorizationPolicyBuilder.Build();
});

// Configure usage of forwarded headers (Nginx proxy)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All;
    // Clearing known networks/proxies lets it accept headers from any proxy (safe within Docker network)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddApiEndpoints();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "OpenShort API", Version = "v1" });

    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "API Key authentication using the X-Api-Key header. Example: \"SecretDevKey\"",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Name = "X-Api-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Scheme = "ApiKeyScheme"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// Critical: Process Forwarded Headers (X-Forwarded-For, X-Forwarded-Proto) from Nginx
app.UseForwardedHeaders();

// Global Exception Handler - returns RFC 7807 for unhandled exceptions
app.UseExceptionHandler(); 

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStatusCodePages();



app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRoot),
    RequestPath = ""
});

app.UseRouting(); // Explicitly add routing

app.UseAuthentication(); // Ensure Authentication Middleware is called
app.UseAuthorization();

// app.UseIdentityApi cannot be used directly with sharing IdentityUser, so we skip for now
// Let's use MapIdentityApi<IdentityUser>();
// app.MapGroup("/api/auth").MapIdentityApi<Microsoft.AspNetCore.Identity.IdentityUser>();
// Let's use MapIdentityApi<IdentityUser>();
// app.MapGroup("/api/auth").MapIdentityApi<Microsoft.AspNetCore.Identity.IdentityUser>();



// Seed initial data and apply migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Auto-apply migrations
        logger.LogInformation("Applying database migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");

        // Only seed if database is accessible and not already seeded
        if (context.Database.CanConnect())
        {
            logger.LogInformation("Checking if initial data seeding is required...");
            await DbSeeder.SeedAsync(context, userManager);
            logger.LogInformation("Data seeding check completed.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database migration or seeding.");
        // Don't fail startup for seeding errors in production
    }
}



app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
