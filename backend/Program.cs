using System.Text;
using System.Threading.RateLimiting;
using Dapper;
using MechanicApp.Server.Middleware;
using MechanicApp.Server.Options;
using MechanicApp.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Scalar.AspNetCore;

// Register Dapper type handler for DateOnly (not natively supported by Dapper)
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:4201",
                    "http://localhost:5236")
                  .WithHeaders("Authorization", "Content-Type", "X-Tenant-Slug")
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS");
        });
});

// Strongly-typed settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection(StripeSettings.SectionName));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Configuration 'Jwt:Key' is required. Set it via environment variable Jwt__Key or appsettings.");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero // No tolerance for token expiration
    };
});

builder.Services.AddAuthorization();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login: 5 attempts per minute per IP (brute-force protection)
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // Webhook: 30 per minute per IP
    options.AddFixedWindowLimiter("webhook", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // Public unauthenticated endpoints: 30 per minute per IP
    options.AddFixedWindowLimiter("public", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // File uploads: 10 per minute per IP
    options.AddFixedWindowLimiter("upload", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // General authenticated endpoints: 60 per minute per IP
    options.AddFixedWindowLimiter("authenticated", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
    });

    // Global concurrency limiter per IP (DDoS backstop)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetConcurrencyLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = 20,
                QueueLimit = 5
            }));
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    })
    .AddJsonOptions(options =>
    {
        // Treat DateTime values with Unspecified kind (from PostgreSQL TIMESTAMP columns)
        // as UTC so the frontend receives ISO-8601 strings ending in "Z" and can apply
        // the tenant's timezone correctly via the localDate pipe.
        options.JsonSerializerOptions.Converters.Add(new MechanicApp.Server.Converters.UtcDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new MechanicApp.Server.Converters.NullableUtcDateTimeConverter());
    });

// Register NpgsqlDataSource as singleton (connection pool — default/fallback)
var connString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required."); ;
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connString);
builder.Services.AddSingleton(dataSourceBuilder.Build());

// Register services (DI)
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IDbService, DbService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IOrderCalculationService, OrderCalculationService>();
builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
builder.Services.AddScoped<IPaymentDistributionService, PaymentDistributionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<ITenantProvisioningService, TenantProvisioningService>();
// Named HttpClient for Resend API — pooled, pre-configured with auth header.
builder.Services.AddHttpClient("resend", (sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ResendApiKey);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddSingleton<IEmailService, EmailService>();

// OpenAPI document generation (built-in .NET 10)
builder.Services.AddOpenApi();

var app = builder.Build();

// Enable OpenAPI + Scalar interactive API docs in all environments
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("MechanicApp API")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// Restore real client IP from X-Forwarded-For when running behind a reverse proxy (nginx/Traefik)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowAngularDev");

app.UseRateLimiter();

// Ensure wwwroot/uploads directory exists at startup
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);

// Serve uploaded files (logos, photos, etc.)
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Resolve tenant from subdomain/header/JWT before any business logic
app.UseMiddleware<TenantResolutionMiddleware>();

// Check subscription status before allowing API access
app.UseMiddleware<SubscriptionMiddleware>();

app.MapControllers();

app.Run();
