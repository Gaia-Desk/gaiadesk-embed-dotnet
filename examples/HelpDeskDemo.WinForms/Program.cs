// HelpDeskDemo: a WinForms app with a "Get help" button.
//
//   set GAIADESK_EMBED_TOKEN=gdemb_…   (optional: set GAIADESK_SERVER=https://gaiadesk.net)
//   dotnet run --project examples/HelpDeskDemo.WinForms
//
// Your backend creates the support session (POST /v1/support/sessions with a
// secret key) and hands the app the embed token; here it comes from the
// environment or the text box. The red "card number" panel is masked: your
// support team sees it painted black.
//
// GAIADESK_DEMO_GUIDED=1 allows guided input (the session must be "cobrowse").
// GAIADESK_DEMO_AUTOSTART=1 presses Get help at launch, and — for automated
// tests only — GAIADESK_DEMO_TEST_CONSENT=1 answers the consent dialog
// "Share" itself. Every event is printed to stdout as one JSON line; stdin
// "stop" ends the session, "pause"/"resume" pause guided input.

namespace HelpDeskDemo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        StartStdinCommands(form);
        Application.Run(form);
    }

    private static void StartStdinCommands(MainForm form)
    {
        var reader = new Thread(() =>
        {
            string? line;
            try
            {
                while ((line = Console.In.ReadLine()) is not null)
                {
                    var cmd = line.Trim();
                    if (cmd.Length == 0 || !form.IsHandleCreated)
                    {
                        continue;
                    }
                    form.BeginInvoke(() => form.Command(cmd));
                }
            }
            catch (IOException)
            {
                // No stdin (started from Explorer): nothing to read.
            }
        })
        { IsBackground = true, Name = "stdin" };
        reader.Start();
    }
}
