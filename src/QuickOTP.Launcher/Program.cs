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
        if (!File.Exists(appExe))
            appExe = Path.Combine(appDir, "QuickOTP.exe");

        try
        {
            UpdateService.CheckAndApply(root);
        }
        catch (Exception ex)
        {
            try
            {
                MessageBox.Show(
                    "Mise a jour impossible (l'app va demarrer quand meme).\n\n" + ex.Message,
                    "QuickOTP",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch
            {
                Debug.WriteLine(ex);
            }
        }

        // Re-resolve after update may have replaced files
        appExe = Path.Combine(appDir, "QuickOTPApp.exe");
        if (!File.Exists(appExe))
            appExe = Path.Combine(appDir, "QuickOTP.exe");

        if (!File.Exists(appExe))
        {
            MessageBox.Show(
                "Fichiers de l'application introuvables (dossier app/).\nReinstallez QuickOTP.",
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

        if (string.IsNullOrWhiteSpace(manifest.Sha256))
            throw new InvalidOperationException("Manifeste invalide: sha256 manquant.");

        var localVersion = ReadLocalVersion(installRoot);
        if (!IsNewer(manifest.Version, localVersion))
            return;

        ApplyUpdate(installRoot, manifest);
        MessageBox.Show(
            $"QuickOTP a ete mis a jour ({localVersion} -> {manifest.Version}).",
            "QuickOTP",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static string ResolveFeedUrl(string installRoot)
    {
        var local = Path.Combine(installRoot, "update-feed.url");
        if (File.Exists(local))
        {
            var url = File.ReadAllText(local).Trim().TrimStart('\uFEFF');
            if (url.Length > 0 && !url.StartsWith('#'))
                return url;
        }

        return "https://github.com/bipowww/QuickOTP/releases/latest/download/update.json";
    }

    private static string ReadLocalVersion(string installRoot)
    {
        var path = Path.Combine(installRoot, "version.txt");
        if (File.Exists(path))
        {
            var v = File.ReadAllText(path).Trim().TrimStart('\uFEFF');
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
        var json = resp.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult().Trim().TrimStart('\uFEFF');
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
        if (!Uri.TryCreate(manifest.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("URL de mise a jour invalide (HTTPS requis).");

        var tempDir = Path.Combine(Path.GetTempPath(), "QuickOTP-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var zipPath = Path.Combine(tempDir, "update.zip");
            Download(manifest.Url!, zipPath);

            var hash = ComputeSha256(zipPath);
            if (!hash.Equals(manifest.Sha256!.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Controle d'integrite SHA256 echoue.");

            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            if (!File.Exists(Path.Combine(extractDir, "QuickOTP.exe")) &&
                Directory.Exists(Path.Combine(extractDir, "app")))
            {
                ReplaceDirectory(Path.Combine(extractDir, "app"), Path.Combine(installRoot, "app"));
            }
            else
            {
                foreach (var file in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(extractDir, file);
                    // Never pull user secrets from a package
                    var name = Path.GetFileName(rel);
                    if (name.Equals("client_secret.json", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("accounts.json", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".dpapi", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dest = Path.Combine(installRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    if (rel.Equals("QuickOTP.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(file, dest + ".new", overwrite: true);
                        continue;
                    }
                    File.Copy(file, dest, overwrite: true);
                }

                ScheduleLauncherSwap(installRoot);
            }

            File.WriteAllText(Path.Combine(installRoot, "version.txt"), manifest.Version!.Trim());
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    private static void ScheduleLauncherSwap(string installRoot)
    {
        var pending = Path.Combine(installRoot, "QuickOTP.exe.new");
        if (!File.Exists(pending))
            return;

        var bat = Path.Combine(installRoot, "_apply_launcher.bat");
        var launcher = Path.Combine(installRoot, "QuickOTP.exe");
        File.WriteAllText(bat,
            "@echo off\r\n" +
            "setlocal\r\n" +
            "cd /d \"%~dp0\"\r\n" +
            "set /a n=0\r\n" +
            ":retry\r\n" +
            "timeout /t 1 /nobreak >nul\r\n" +
            "move /y \"QuickOTP.exe.new\" \"QuickOTP.exe\" >nul 2>&1\r\n" +
            "if not exist \"QuickOTP.exe.new\" goto done\r\n" +
            "set /a n+=1\r\n" +
            "if %n% LSS 15 goto retry\r\n" +
            ":done\r\n" +
            "del \"%~f0\" >nul 2>&1\r\n");
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{bat}\"",
            WorkingDirectory = installRoot,
            CreateNoWindow = true,
            UseShellExecute = false
        });
        _ = launcher;
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
            if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                continue;
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
