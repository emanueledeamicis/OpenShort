using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OpenShort.Infrastructure.Data;
using OpenShort.Core.Interfaces;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Use fixed version to allow migrations without running DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Parse("8.0.30-mysql")));

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

builder.Services.AddIdentityCore<Microsoft.AspNetCore.Identity.IdentityUser>()
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStatusCodePages();


app.UseAuthentication(); // Ensure Authentication Middleware is called
app.UseAuthorization();

// app.UseIdentityApi cannot be used directly with AddIdentityCore easily without mapping endpoints manually or using AddIdentityApiEndpoints
// Let's use MapIdentityApi<IdentityUser>();
// app.MapGroup("/api/auth").MapIdentityApi<Microsoft.AspNetCore.Identity.IdentityUser>();

app.MapControllers();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
    
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    // Retry logic for Docker - MySQL might not be ready immediately
    int maxRetries = 15;
    int retryDelayMs = 2000;
    bool initializationSuccessful = false;
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            logger.LogInformation("Attempting to connect to database (attempt {Attempt}/{MaxRetries})...", i + 1, maxRetries);
            
            if (context.Database.CanConnect())
            {
                logger.LogInformation("Database connection successful. Applying migrations...");
                context.Database.Migrate();
                logger.LogInformation("Migrations applied successfully. Seeding data...");
                await DbSeeder.SeedAsync(context, userManager);
                logger.LogInformation("Database initialization completed successfully.");
                initializationSuccessful = true;
                break;
            }
            else 
            {
                    logger.LogWarning("Database.CanConnect() returned false. Retrying...");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database initialization attempt {Attempt} failed. Retrying in {Delay}ms...", i + 1, retryDelayMs);
        }

        if (i < maxRetries - 1)
        {
            await Task.Delay(retryDelayMs);
        }
    }

    if (!initializationSuccessful)
    {
        throw new Exception($"Failed to connect to database and apply migrations after {maxRetries} attempts.");
    }
}

app.Run();
