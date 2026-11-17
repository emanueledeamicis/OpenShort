using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OpenShort.Infrastructure.Data;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;
using OpenShort.Core;
using System.Threading.Channels;
using OpenShort.Core.Entities;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseProvider = builder.Configuration["DatabaseProvider"];

// Check for individual MYSQL_ configurations from Environment, User Secrets, or appsettings
var mysqlHost = builder.Configuration["MYSQL_HOST"];
if (!string.IsNullOrEmpty(mysqlHost))
{
    // The values are escaped by the factory: they are never concatenated into the connection string
    connectionString = MySqlConnectionStringFactory.Create(
        mysqlHost,
        builder.Configuration[MySqlConnectionStringFactory.PortSettingName],
        builder.Configuration["MYSQL_DATABASE"],
        builder.Configuration["MYSQL_USER"],
        builder.Configuration["MYSQL_PASSWORD"]);
    databaseProvider = "MySql"; // Force provider if env vars are present
}

// Determine provider based on configuration or connection string format
bool useMySql = string.Equals(databaseProvider, "MySql", StringComparison.OrdinalIgnoreCase) || 
                (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase));

if (useMySql)
{
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
    
    builder.Services.AddDbContext<AppDbContext, SqliteDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Logged once the app is built. The connection string itself is never logged: it may contain the password
var databaseDescription = DatabaseConnectionDescriber.Describe(connectionString, useMySql);

// Configure settings
builder.Services.Configure<SlugSettings>(builder.Configuration.GetSection("SlugSettings"));
builder.Services.Configure<GitHubUpdateOptions>(builder.Configuration.GetSection("GitHubUpdate"));

// Caching Configuration
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ISlugGenerator, SlugGenerator>();
builder.Services.AddScoped<IDomainService, DomainService>();
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>(); // Register Token Service
builder.Services.AddScoped<IApiKeyService, ApiKeyService>(); // Register ApiKey Service
builder.Services.AddScoped<ISettingService, SettingService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

// Register JWT Key Provider
builder.Services.AddScoped<IJwtKeyProvider, JwtKeyProvider>();

// --- CLICK TRACKING CHANNEL ---
// Create a bounded channel to prevent out-of-memory errors if DB is down.
builder.Services.AddSingleton(Channel.CreateBounded<ClickEvent>(new BoundedChannelOptions(10000)
{
    FullMode = BoundedChannelFullMode.DropOldest
}));
builder.Services.AddSingleton(provider => provider.GetRequiredService<Channel<ClickEvent>>().Reader);
builder.Services.AddSingleton(provider => provider.GetRequiredService<Channel<ClickEvent>>().Writer);
builder.Services.AddHostedService<ClickTrackingBackgroundService>();

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
        // The IssuerSigningKey check is handled dynamically in Events
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ClockSkew = TimeSpan.Zero
    };
    
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = async context =>
        {
            var jwtProvider = context.HttpContext.RequestServices.GetRequiredService<IJwtKeyProvider>();
            var secretKey = await jwtProvider.GetOrGenerateKeyAsync();
            
            context.Options.TokenValidationParameters.IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey));
        }
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

// Forwarded headers are accepted only from the bundled Nginx (loopback) and from the declared reverse proxies
// Parsed here so that an invalid value stops the startup before anything else happens
var trustedProxies = OpenShort.Api.Auth.TrustedProxies.Parse(
    builder.Configuration[OpenShort.Api.Auth.TrustedProxies.ConfigurationKey]);
builder.Services.Configure<ForwardedHeadersOptions>(trustedProxies.Configure);

builder.Services.AddIdentityCore<AppUser>(OpenShort.Api.Auth.AuthProtection.ConfigureIdentity)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddApiEndpoints();

// Delays repeated failed logins; state is in memory, hence the singleton
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<OpenShort.Api.Auth.LoginThrottle>();

// Limits the request rate on the endpoints marked with the auth policy (login, admin setup)
builder.Services.AddRateLimiter(OpenShort.Api.Auth.AuthProtection.ConfigureRateLimiter);

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

app.Logger.LogInformation("Using database provider: {Database}", databaseDescription);

app.Logger.LogInformation(
    "Forwarded headers are trusted from loopback and from: {TrustedProxies}",
    trustedProxies.Entries.Count > 0 ? string.Join(", ", trustedProxies.Entries) : "(no other proxy declared)");
if (trustedProxies.TrustsEveryAddress)
{
    app.Logger.LogWarning(
        "{ConfigurationKey} trusts every address: any client can choose the IP address it is seen with",
        OpenShort.Api.Auth.TrustedProxies.ConfigurationKey);
}

// Configure the HTTP request pipeline.

// Critical: Process Forwarded Headers (X-Forwarded-For, X-Forwarded-Proto) from trusted proxies
OpenShort.Api.Auth.TrustedProxies.Use(app);

// Global Exception Handler - returns RFC 7807 for unhandled exceptions
app.UseExceptionHandler(); 

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStatusCodePages();

app.UseRouting(); // Explicitly add routing

app.UseRateLimiter(); // After routing: the policies are attached to the endpoints

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
    var logger = services.GetRequiredService<ILogger<Program>>();
    var initializer = services.GetRequiredService<DatabaseInitializer>();
    
    try
    {
        await initializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database migration or seeding.");
        throw;
    }
}



app.MapControllers();

app.Run();
