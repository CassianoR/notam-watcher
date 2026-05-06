using Microsoft.EntityFrameworkCore;
using NotamWatcher.Api.BackgroundServices;
using NotamWatcher.Api.Endpoints;
using NotamWatcher.Api.Hubs;
using NotamWatcher.Infrastructure;
using NotamWatcher.Infrastructure.Persistence;
using NotamWatcher.Parsing;
using Serilog;
using Serilog.Events;

// Bootstrap Serilog early so startup errors are captured.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

        // Rolling file in non-development (i.e., Docker / production).
        if (!ctx.HostingEnvironment.IsDevelopment())
        {
            config.WriteTo.File(
                path: "logs/notam-watcher-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
        }
    });

    // ── Services ──────────────────────────────────────────────────────────────
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSingleton<INotamParser, NotamParser>();
    builder.Services.AddHostedService<NotamFetcherService>();

    builder.Services.AddSignalR();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new() { Title = "NOTAM Watcher API", Version = "v1" });
    });

    // CORS: Angular dev server on 4200, production build on 80.
    builder.Services.AddCors(opts =>
    {
        opts.AddPolicy("Angular", policy =>
        {
            var origins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? new[] { "http://localhost:4200" };

            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // required for SignalR WebSocket negotiation
        });
    });

    var app = builder.Build();

    // ── Migrate DB on startup ─────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    // ── Middleware ────────────────────────────────────────────────────────────
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.000}ms)";
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("Angular");

    // ── Endpoints ─────────────────────────────────────────────────────────────
    app.MapRouteEndpoints();
    app.MapNotamEndpoints();
    app.MapHub<NotamHub>("/hubs/notams");

    app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }))
        .WithTags("Health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
