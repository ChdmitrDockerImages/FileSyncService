using System.Net.Http.Headers;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileSyncServer
{
    public class SyncService : BackgroundService
    {
        private readonly FileSyncConfig _cfg;
        private readonly ILogger<SyncService> _log;
        private readonly HttpClient _client = new();

        public SyncService(FileSyncConfig cfg, ILogger<SyncService> log)
        {
            _cfg = cfg;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var schedules = _cfg.Config.Sync.Schedule
                .Select(s => CronExpression.Parse(s))
                .ToList();

            _log.LogInformation("Sync scheduler started with {Count} cron rules", schedules.Count);

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var nextRuns = schedules
                    .Select(c => c.GetNextOccurrence(now))
                    .Where(t => t.HasValue)
                    .Select(t => t!.Value)
                    .ToList();

                var delay = nextRuns.Any() ? nextRuns.Min() - now : TimeSpan.FromHours(12);
                if (delay < TimeSpan.Zero) delay = TimeSpan.FromMinutes(1);

                _log.LogInformation("Next sync in {Delay}", delay);
                await Task.Delay(delay, stoppingToken);

                await SyncAll();
            }
        }

        public async Task SyncAll()
        {
            _log.LogInformation("Starting synchronization...");

            foreach (var category in _cfg.Files.Mirror)
            {
                var dir = Path.Combine("/data/mirror/", category.Key);
                Directory.CreateDirectory(dir);

                foreach (var kv in category.Value)
                {
                    var localFile = Path.Combine(dir, kv.Key);
                    var remoteUrl = kv.Value;

                    await SyncFile(localFile, remoteUrl);
                }
            }

            _log.LogInformation("Synchronization finished.");
        }

        private async Task SyncFile(string localPath, string url)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);

                if (File.Exists(localPath))
                {
                    var info = new FileInfo(localPath);
                    req.Headers.IfModifiedSince = info.LastWriteTimeUtc;
                }

                var resp = await _client.SendAsync(req);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    _log.LogDebug("No update for {File}", localPath);
                    return;
                }

                resp.EnsureSuccessStatusCode();
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localPath, bytes);
                _log.LogInformation("Updated {File} ({Size} bytes)", localPath, bytes.Length);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error syncing {File}", localPath);
            }
        }
    }
}
