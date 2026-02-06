using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OpenShort.Infrastructure.Data;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;
using OpenShort.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Use fixed version to allow migrations without running DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Parse("8.0.30-mysql")));

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


app.UseAuthentication(); // Ensure Authentication Middleware is called
app.UseAuthorization();

// app.UseIdentityApi cannot be used directly with AddIdentityCore easily without mapping endpoints manually or using AddIdentityApiEndpoints
// Let's use MapIdentityApi<IdentityUser>();
// app.MapGroup("/api/auth").MapIdentityApi<Microsoft.AspNetCore.Identity.IdentityUser>();

app.MapControllers();

// Seed initial data (migrations are handled by init container)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
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
        logger.LogError(ex, "An error occurred during data seeding.");
        // Don't fail startup for seeding errors in production
    }
}

app.Run();
