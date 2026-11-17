using System.IO.Compression;
using System.Security.Cryptography;

namespace FlashEditor.AI.Models;

/// <summary>
/// Finds AI model files and downloads them once.
/// <para>
/// Everything lives in ONE place: the "models" folder inside the app's own folder (next to
/// FlashEditor.exe). Nothing is stored in AppData or anywhere else, so the whole app (with its models)
/// is a single folder that can be copied, backed up or uninstalled in one go.
/// </para>
/// Downloads go to a ".part" file and are checked (size and SHA-256) before use, so a broken or
/// half-finished download is never loaded.
/// </summary>
public sealed class ModelStore
{
    private readonly string[] _searchFolders;

    /// <param name="folder">Use this folder instead (for tests). Null = the app's own "models" folder.</param>
    public ModelStore(string? folder = null)
    {
        Folder = folder ?? Path.Combine(AppContext.BaseDirectory, "models");
        _searchFolders = new[] { Folder };
    }

    /// <summary>Where downloads are saved.</summary>
    public string Folder { get; }

    /// <summary>The installed model file, or (when missing) where a download would be saved.</summary>
    public string PathOf(ModelInfo model)
        => FindInstalled(model) ?? Path.Combine(Folder, model.Folder ?? "", model.FileName);

    /// <summary>True when the model file is present (in any searched folder) and has the expected size.</summary>
    public bool IsInstalled(ModelInfo model) => FindInstalled(model) != null;

    private string? FindInstalled(ModelInfo model)
    {
        foreach (var folder in _searchFolders)
        {
            if (model.ArchiveFiles is { } files)
            {
                // Unpacked model: every file must be there with the right size.
                string dir = Path.Combine(folder, model.Folder ?? "");
                bool complete = files.All(f => new FileInfo(Path.Combine(dir, f.Key)) is { Exists: true } fi && fi.Length == f.Value);
                if (complete) return Path.Combine(dir, model.FileName);
                continue;
            }
            var file = new FileInfo(Path.Combine(folder, model.FileName));
            if (file.Exists && file.Length == model.SizeBytes) return file.FullName;
        }
        return null;
    }

    /// <summary>Downloads the model into <see cref="Folder"/>. <paramref name="progress"/> receives 0..1.</summary>
    public async Task DownloadAsync(ModelInfo model, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, ".write-test"), Array.Empty<byte>());
            File.Delete(Path.Combine(Folder, ".write-test"));
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                $"Flash Editor cannot save the AI model in its folder:\n{Folder}\n\n" +
                "Reinstall Flash Editor with its installer (it allows saving models there), or start it once as administrator.");
        }
        string target = Path.Combine(Folder, model.FileName);
        string part = model.ArchiveFiles != null ? Path.Combine(Folder, model.Folder + ".zip.part") : target + ".part";

        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FlashEditor/1.0");
            using var response = await http.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(cancellation);
            await using var output = File.Create(part);
            var buffer = new byte[1 << 16];
            long done = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellation)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                done += read;
                progress?.Report(Math.Min(1.0, (double)done / model.SizeBytes));
            }
        }

        Verify(model, part);
        if (model.ArchiveFiles != null)
        {
            Unpack(model, part);
            TryDelete(part);
            return;
        }
        File.Move(part, target, overwrite: true);
    }

    /// <summary>Throws <see cref="InvalidDataException"/> when the file is not the expected model.</summary>
    public static void Verify(ModelInfo model, string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length != model.SizeBytes)
        {
            TryDelete(path);
            throw new InvalidDataException(
                $"The downloaded model has the wrong size ({(file.Exists ? file.Length : 0)} bytes, expected {model.SizeBytes}). Please try again.");
        }
        if (string.IsNullOrEmpty(model.Sha256)) return;

        using var stream = File.OpenRead(path);
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        if (!hash.Equals(model.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            stream.Dispose();
            TryDelete(path);
            throw new InvalidDataException("The downloaded model is damaged (checksum mismatch). Please try again.");
        }
    }

    /// <summary>Takes only the listed model files out of the (already verified) zip.</summary>
    private void Unpack(ModelInfo model, string zipPath)
    {
        string dir = Path.Combine(Folder, model.Folder ?? "");
        Directory.CreateDirectory(dir);
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var (name, size) in model.ArchiveFiles!)
        {
            var entry = zip.Entries.FirstOrDefault(e => e.Name == name)
                ?? throw new InvalidDataException($"The downloaded model is missing {name}.");
            if (entry.Length != size) throw new InvalidDataException($"{name} in the download has an unexpected size.");
            entry.ExtractToFile(Path.Combine(dir, name), overwrite: true);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
