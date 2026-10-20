using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FactuurWizard.App.Services;

public enum MailResultaat
{
    /// <summary>Het mailprogramma heeft een nieuw bericht met bijlage geopend.</summary>
    Geopend,
    /// <summary>De gebruiker heeft het bericht geannuleerd.</summary>
    Geannuleerd,
    /// <summary>Geen MAPI-mailprogramma: een leeg bericht is geopend en de bijlage staat klaar in Verkenner.</summary>
    ZonderBijlage,
}

/// <summary>
/// Opent een nieuw bericht in het eigen mailprogramma van de gebruiker (bijv. klassiek Outlook of Thunderbird)
/// via Simple MAPI, met de PDF als bijlage. De gebruiker verstuurt het bericht zelf.
/// </summary>
public class Mailer
{
    private const int MapiLogonUi = 0x1;
    private const int MapiDialog = 0x8;
    private const int MapiTo = 1;
    private const int SuccessSuccess = 0;
    private const int MapiUserAbort = 1;

    public async Task<MailResultaat> NieuwBerichtAsync(string? aan, string onderwerp, string tekst, string bijlage)
    {
        // Simple MAPI blokkeert tot het opstelvenster gesloten is (bij sommige programma's); daarom op een eigen STA-thread.
        var resultaat = await Task.Factory.StartNew(() => VerstuurViaMapi(aan, onderwerp, tekst, bijlage),
            CancellationToken.None, TaskCreationOptions.LongRunning, StaTaakplanner.Instantie);

        switch (resultaat)
        {
            case SuccessSuccess:
                return MailResultaat.Geopend;
            case MapiUserAbort:
                return MailResultaat.Geannuleerd;
            default:
                OpenMailto(aan, onderwerp, tekst);
                FactuurDocumenten.ToonInVerkenner(bijlage);
                return MailResultaat.ZonderBijlage;
        }
    }

    private static void OpenMailto(string? aan, string onderwerp, string tekst)
    {
        var url = $"mailto:{Uri.EscapeDataString(aan ?? "")}?subject={Uri.EscapeDataString(onderwerp)}" +
                  $"&body={Uri.EscapeDataString(tekst.Replace("\n", "\r\n"))}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static int VerstuurViaMapi(string? aan, string onderwerp, string tekst, string bijlage)
    {
        var ontvangers = IntPtr.Zero;
        var bestanden = IntPtr.Zero;
        try
        {
            var bericht = new MapiMessage { Subject = onderwerp, NoteText = tekst };

            if (!string.IsNullOrWhiteSpace(aan))
            {
                ontvangers = Marshal.AllocHGlobal(Marshal.SizeOf<MapiRecipDesc>());
                Marshal.StructureToPtr(new MapiRecipDesc { RecipClass = MapiTo, Name = aan, Address = "SMTP:" + aan }, ontvangers, false);
                bericht.RecipCount = 1;
                bericht.Recips = ontvangers;
            }

            bestanden = Marshal.AllocHGlobal(Marshal.SizeOf<MapiFileDesc>());
            Marshal.StructureToPtr(new MapiFileDesc { Position = -1, PathName = bijlage, FileName = Path.GetFileName(bijlage) }, bestanden, false);
            bericht.FileCount = 1;
            bericht.Files = bestanden;

            return MAPISendMailW(IntPtr.Zero, IntPtr.Zero, bericht, MapiLogonUi | MapiDialog, 0);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
        {
            Foutlog.Schrijf(ex);
            return -1;
        }
        finally
        {
            if (ontvangers != IntPtr.Zero)
            {
                Marshal.DestroyStructure<MapiRecipDesc>(ontvangers);
                Marshal.FreeHGlobal(ontvangers);
            }
            if (bestanden != IntPtr.Zero)
            {
                Marshal.DestroyStructure<MapiFileDesc>(bestanden);
                Marshal.FreeHGlobal(bestanden);
            }
        }
    }

    [DllImport("MAPI32.DLL", CharSet = CharSet.Unicode)]
    private static extern int MAPISendMailW(IntPtr session, IntPtr hwnd, MapiMessage message, int flags, int reserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class MapiMessage
    {
        public int Reserved;
        public string? Subject;
        public string? NoteText;
        public string? MessageType;
        public string? DateReceived;
        public string? ConversationId;
        public int Flags;
        public IntPtr Originator;
        public int RecipCount;
        public IntPtr Recips;
        public int FileCount;
        public IntPtr Files;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class MapiRecipDesc
    {
        public int Reserved;
        public int RecipClass;
        public string? Name;
        public string? Address;
        public int EntryIdSize;
        public IntPtr EntryId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class MapiFileDesc
    {
        public int Reserved;
        public int Flags;
        public int Position;
        public string? PathName;
        public string? FileName;
        public IntPtr FileType;
    }

    /// <summary>Voert taken uit op een nieuwe STA-thread, zoals MAPI-clients verwachten.</summary>
    private sealed class StaTaakplanner : TaskScheduler
    {
        public static readonly StaTaakplanner Instantie = new();

        protected override void QueueTask(Task task)
        {
            var thread = new Thread(() => TryExecuteTask(task)) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
        protected override IEnumerable<Task>? GetScheduledTasks() => null;
    }
}
