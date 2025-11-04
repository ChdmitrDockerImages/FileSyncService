using Microsoft.Extensions.FileProviders;

namespace FileSyncServer;

public static class FileServerExtensions
{
    public static void MapStaticFiles(this WebApplication app, FileSyncConfig cfg)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(cfg.Files.Public),
            RequestPath = "/public"
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(cfg.Files.Private),
            RequestPath = "/private"
        });

        var mirrorRoot = Path.Combine("/data/mirror/");
        if (!Directory.Exists(mirrorRoot))
            Directory.CreateDirectory(mirrorRoot);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(mirrorRoot),
            RequestPath = "/mirror"
        });
    }
}
