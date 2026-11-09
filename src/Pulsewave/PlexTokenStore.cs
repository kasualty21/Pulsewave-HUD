using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Pulsewave;

/// <summary>
/// Keeps the Plex token out of settings.json and its backups. The file is encrypted
/// with Windows DPAPI for the current user, so other accounts and copies of the file
/// on another PC cannot read it.
/// </summary>
internal static class PlexTokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Pulsewave Plex token v1");
    public static string FilePath => Path.Combine(SettingsStore.DirectoryPath, "plex-token.dat");

    // Plex tokens are short URL-safe strings; reject anything else before storing.
    public static bool IsPlausible(string? token) =>
        token is { Length: >= 10 and <= 128 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static string? Load() => LoadFile(FilePath);

    internal static string? LoadFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            var token = Encoding.UTF8.GetString(plain);
            return IsPlausible(token) ? token : null;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return null; // Unreadable or from another account: behave as if no token is set.
        }
    }

    public static void Save(string token) => SaveFile(FilePath, token);

    internal static void SaveFile(string path, string token)
    {
        if (!IsPlausible(token)) throw new ArgumentException("That does not look like a Plex token.", nameof(token));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(staging, protectedBytes);
            File.Move(staging, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }

    public static void Delete() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
