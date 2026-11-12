using System.Diagnostics;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace Pulsewave;

/// <summary>Lets the user paste a Plex token for Plexamp now playing. The saved token is never shown again.</summary>
public sealed class PlexTokenForm : Forms.Form
{
    /// <summary>The Halo desktop look (DesktopStyle), applied once everything in the window exists.</summary>
    protected override void OnLoad(EventArgs e) { DesktopStyle.Apply(this); base.OnLoad(e); }

    private const string HelpUrl = "https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/";

    public PlexTokenForm(Action<string?> onChanged)
    {
        Text = "Lumen HUD · Plexamp now playing";
        StartPosition = Forms.FormStartPosition.CenterScreen;
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(560, 290);
        BackColor = Color.FromArgb(19, 27, 37);
        ForeColor = Color.FromArgb(232, 242, 249);
        Font = new Font("Segoe UI", 9);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        bool hasToken = PlexTokenStore.Load() != null;
        var intro = new Forms.Label
        {
            Location = new Point(18, 16), Size = new Size(524, 112),
            Text = "Plexamp reports what it is playing on this PC, but the title, artist and art come from your " +
                   "Plex server, which needs your Plex token.\n\n" +
                   "The token is encrypted for your Windows account and sent only to the Plex server that Plexamp " +
                   "is playing from (a plex.direct or local network address).\n\n" +
                   (hasToken ? "A token is saved. Paste a new one to replace it." : "No token is saved yet.")
        };
        var help = new Forms.LinkLabel
        {
            Location = new Point(18, 132), AutoSize = true, Text = "How to find your Plex token (support.plex.tv)",
            LinkColor = Color.FromArgb(112, 232, 211), ActiveLinkColor = Color.White
        };
        help.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true });
        var input = new Forms.TextBox
        {
            Location = new Point(18, 164), Width = 524, UseSystemPasswordChar = true,
            PlaceholderText = "Paste X-Plex-Token"
        };
        var status = new Forms.Label { Location = new Point(18, 194), Size = new Size(524, 22), ForeColor = Color.FromArgb(255, 191, 110) };

        var save = new Forms.Button { Text = "Save", Location = new Point(442, 236), Size = new Size(100, 34) };
        var cancel = new Forms.Button { Text = "Cancel", Location = new Point(334, 236), Size = new Size(100, 34), DialogResult = Forms.DialogResult.Cancel };
        var remove = new Forms.Button { Text = "Remove saved token", Location = new Point(18, 236), Size = new Size(160, 34), Enabled = hasToken };
        save.Click += (_, _) =>
        {
            string token = input.Text.Trim();
            if (!PlexTokenStore.IsPlausible(token)) { status.Text = "That does not look like a Plex token (letters, digits, - and _ only)."; return; }
            try { PlexTokenStore.Save(token); onChanged(token); Close(); }
            catch (Exception ex) { status.Text = "Could not save the token: " + ex.Message; }
        };
        remove.Click += (_, _) =>
        {
            try { PlexTokenStore.Delete(); onChanged(null); Close(); }
            catch (Exception ex) { status.Text = "Could not remove the token: " + ex.Message; }
        };
        Controls.AddRange([intro, help, input, status, remove, cancel, save]);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
