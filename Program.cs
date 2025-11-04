using System.Net;
using System.Security.Cryptography.X509Certificates;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FileSyncServer;

var yaml = File.ReadAllText("config.yml");
var deserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();

var cfg = deserializer.Deserialize<FileSyncConfig>(yaml);

// --- подставляем переменные окружения ---
foreach (var auth in cfg.Config.Auth)
{
    if (auth.Username.StartsWith("env."))
        auth.Username = Environment.GetEnvironmentVariable(auth.Username["env.".Length..]) ?? "";
    if (auth.Password.StartsWith("env."))
        auth.Password = Environment.GetEnvironmentVariable(auth.Password["env.".Length..]) ?? "";
}

var builder = WebApplication.CreateBuilder(args);

// ---------- ЛОГИРОВАНИЕ ----------
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(opt =>
{
    opt.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
    opt.SingleLine = true;
});

// путь к лог-файлу и параметры ротации
var logPath = cfg.Config.Log.Path;
if (string.IsNullOrWhiteSpace(logPath))
{
    Directory.CreateDirectory("logs");
    logPath = Path.Combine("logs", "filesync.log");
}

var rotation = cfg.Config.Log.Rotation ?? new FileSyncConfig.ConfigSection.LogSection.RotationSection();
builder.Logging.AddFile(logPath, rotation.MaxSizeMb, rotation.MaxFiles);

builder.WebHost.ConfigureKestrel(opt =>
{
    var cert = X509Certificate2.CreateFromPemFile(
        cfg.Config.Https.CertPathPublic,
        cfg.Config.Https.CertPathPrivate);
    opt.ListenAnyIP(cfg.Config.Https.Port, listen => listen.UseHttps(cert));
});

builder.Services.AddSingleton(cfg);
builder.Services.AddSingleton<SyncService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SyncService>());

var app = builder.Build();

app.UseMiddleware<AuthMiddleware>(cfg);
app.MapStaticFiles(cfg);

// --- ручной триггер только с localhost ---
app.MapPost("/sync/now", async (HttpContext ctx, SyncService sync, ILogger<Program> log) =>
{
    var remoteIp = ctx.Connection.RemoteIpAddress;
    if (remoteIp == null || !IPAddress.IsLoopback(remoteIp))
    {
        log.LogWarning("Unauthorized /sync/now access attempt from {IP}", remoteIp);
        return Results.StatusCode(403);
    }

    log.LogInformation("Manual sync triggered from localhost");
    await sync.SyncAll();
    return Results.Ok(new { status = "started", time = DateTime.UtcNow });
});

app.Run();
