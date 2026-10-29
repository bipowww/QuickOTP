using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace QuickOTP.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var root = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var appDir = Path.Combine(root, "app");
        var appExe = Path.Combine(appDir, "QuickOTPApp.exe");
        // Backward compat if older package used QuickOTP.exe inside app/
        if (!File.Exists(appExe))
            appExe = Path.Combine(appDir, "QuickOTP.exe");

        try
        {
            UpdateService.CheckAndApply(root);
        }
        catch (Exception ex)
        {
            // Never block launch on update failure (offline, etc.)
            Debug.WriteLine(ex);
        }

        if (!File.Exists(appExe))
        {
            MessageBox.Show(
                "Fichiers de l'application introuvables (dossier app/).\nRéinstallez QuickOTP.",
                "QuickOTP",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = appExe,
            WorkingDirectory = appDir,
            UseShellExecute = true,
            Arguments = string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
        });
    }
}

internal static class UpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public static void CheckAndApply(string installRoot)
    {
        var feedUrl = ResolveFeedUrl(installRoot);
        if (string.IsNullOrWhiteSpace(feedUrl))
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var manifest = FetchManifest(feedUrl, cts.Token);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Url))
            return;

        var localVersion = ReadLocalVersion(installRoot);
        if (!IsNewer(manifest.Version, localVersion))
            return;

        var result = MessageBox.Show(
            $"Une mise à jour QuickOTP est disponible ({localVersion} → {manifest.Version}).\n\nInstaller maintenant ?",
            "QuickOTP — Mise à jour",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result != DialogResult.Yes)
            return;

        ApplyUpdate(installRoot, manifest);
        MessageBox.Show(
            $"Mise à jour {manifest.Version} installée.",
            "QuickOTP",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static string ResolveFeedUrl(string installRoot)
    {
        var local = Path.Combine(installRoot, "update-feed.url");
        if (File.Exists(local))
        {
            var url = File.ReadAllText(local).Trim();
            if (url.Length > 0 && !url.StartsWith('#'))
                return url;
        }

        // Default public feed (GitHub Releases asset). Override via update-feed.url
        return "https://github.com/bipowww/QuickOTP/releases/latest/download/update.json";
    }

    private static string ReadLocalVersion(string installRoot)
    {
        var path = Path.Combine(installRoot, "version.txt");
        if (File.Exists(path))
        {
            var v = File.ReadAllText(path).Trim();
            if (v.Length > 0) return v;
        }
        return typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static UpdateManifest? FetchManifest(string feedUrl, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        req.Headers.UserAgent.ParseAdd("QuickOTP-Updater/1.0");
        using var resp = Http.Send(req, ct);
        if (!resp.IsSuccessStatusCode)
            return null;
        var json = resp.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
        return JsonSerializer.Deserialize(json, UpdateJsonContext.Default.UpdateManifest);
    }

    private static bool IsNewer(string remote, string local)
    {
        if (!Version.TryParse(Normalize(remote), out var r)) return false;
        if (!Version.TryParse(Normalize(local), out var l)) return true;
        return r > l;
    }

    private static string Normalize(string v)
    {
        v = v.Trim().TrimStart('v', 'V');
        var parts = v.Split('.');
        if (parts.Length == 2) return v + ".0";
        return v;
    }

    private static void ApplyUpdate(string installRoot, UpdateManifest manifest)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QuickOTP-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var zipPath = Path.Combine(tempDir, "update.zip");
            Download(manifest.Url!, zipPath);

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var hash = ComputeSha256(zipPath);
                if (!hash.Equals(manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Contrôle d'intégrité SHA256 échoué.");
            }

            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            // Package layout: zip contains QuickOTP.exe (launcher), update-feed.url, version.txt, app/*
            // Or zip contains only app/* payload — detect both.
            var payloadRoot = extractDir;
            if (!File.Exists(Path.Combine(extractDir, "QuickOTP.exe")) &&
                Directory.Exists(Path.Combine(extractDir, "app")))
            {
                // launcher-less payload: only refresh app/
                ReplaceDirectory(Path.Combine(extractDir, "app"), Path.Combine(installRoot, "app"));
            }
            else
            {
                // Full package: copy everything except don't delete user LocalAppData
                foreach (var file in Directory.GetFiles(payloadRoot, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(payloadRoot, file);
                    var dest = Path.Combine(installRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    // Don't overwrite the running launcher until end — copy to .new then swap via helper
                    if (rel.Equals("QuickOTP.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(file, dest + ".new", overwrite: true);
                        continue;
                    }
                    File.Copy(file, dest, overwrite: true);
                }

                var pending = Path.Combine(installRoot, "QuickOTP.exe.new");
                if (File.Exists(pending))
                {
                    // Schedule replace after exit using cmd
                    var bat = Path.Combine(installRoot, "_apply_launcher.bat");
                    File.WriteAllText(bat,
                        "@echo off\r\n" +
                        "timeout /t 1 /nobreak >nul\r\n" +
                        "move /y \"QuickOTP.exe.new\" \"QuickOTP.exe\" >nul\r\n" +
                        "del \"%~f0\"\r\n");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{bat}\"",
                        WorkingDirectory = installRoot,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }

            File.WriteAllText(Path.Combine(installRoot, "version.txt"), manifest.Version!.Trim());
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private static void ReplaceDirectory(string source, string dest)
    {
        if (Directory.Exists(dest))
        {
            var backup = dest + ".old";
            if (Directory.Exists(backup))
                Directory.Delete(backup, recursive: true);
            Directory.Move(dest, backup);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        CopyDir(source, dest);
        try
        {
            var backup = dest + ".old";
            if (Directory.Exists(backup))
                Directory.Delete(backup, recursive: true);
        }
        catch { /* ignore locked files */ }
    }

    private static void CopyDir(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(dest, rel));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(source, file);
            var target = Path.Combine(dest, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void Download(string url, string dest)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("QuickOTP-Updater/1.0");
        using var resp = Http.Send(req);
        resp.EnsureSuccessStatusCode();
        using var fs = File.Create(dest);
        resp.Content.CopyToAsync(fs).GetAwaiter().GetResult();
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}

internal sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

[JsonSerializable(typeof(UpdateManifest))]
internal partial class UpdateJsonContext : JsonSerializerContext;
