using System.Threading.RateLimiting;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Serilog;
using FanFlow.Api.BackgroundServices;
using FanFlow.Application;
using FanFlow.Infra;
using FanFlow.Ports.Input;

// Bootstrap logger: catches startup failures before configuration is available.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FanFlow API",
        Version = "v1",
        Description = "Starting-point service following the Exeal backend architecture conventions."
    });
});
builder.Services.AddCors();

// Reconfigure logging now that appsettings/environment config is available
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Authentication Services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth:Authority"];
    options.Audience = builder.Configuration["Auth:Audience"];
});

builder.Services.AddAuthorization();

// Public, unauthenticated endpoints must still be rate-limited (keyed by client IP).
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PublicLimiter", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue<int>("RateLimiter:PublicLimiter:PermitLimit"),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, token) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        logger.LogWarning("Rate limit exceeded for {IP}", ip);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too many requests.");
    };
});

// Register application services
builder.Services.AddScoped<IReleaseManager, ReleaseManager>();
builder.Services.AddScoped<IReleaseAnalytics, ReleaseAnalytics>();
builder.Services.AddScoped<ISpamClassifier, SpamClassifier>();
builder.Services.AddScoped<ITrafficTracker, TrafficTracker>();

// Register infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

// Drives async spam classification (see ISpamClassifier) by polling for unclassified events.
builder.Services.AddHostedService<SpamClassificationWorker>();

var app = builder.Build();

// Run database migrations
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    runner.MigrateUp();
}

// Ensure the MinIO bucket used to publish static release pages exists and is publicly readable
await app.Services.EnsurePublishingBucketAsync();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FanFlow API v1");
    });
}

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Example public, unauthenticated endpoint — rate-limited per the architecture convention.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .RequireRateLimiting("PublicLimiter");

app.Run();
