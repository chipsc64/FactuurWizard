using System.IO;

namespace FactuurWizard.App.Services;

public static class Foutlog
{
    public static string Pad { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FactuurWizard", "fouten.log");

    public static void Schrijf(Exception fout)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Pad)!);
            File.AppendAllText(Pad, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {fout}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Loggen mag de applicatie nooit laten vastlopen.
        }
    }
}
