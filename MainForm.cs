/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using libdebug;

namespace Arise_RTM
{
    public sealed class MainForm : Form, IToolHost
    {
        private const int ConnectionTimeoutMs = 10000;
        private const string TargetGameVersion = "1.57";
        private const string GameProcessName = "eboot.bin";
        private const string XUrl = "https://x.com/yAmethxst";
        private const string DiscordUrl = "https://amethxst.dev/discord";
        private const string ZitouneSiteUrl = "https://zitouneanticip.fr";

        private const int PanelHeight = OutfitsPanel.DesiredHeight;
        private const int TabHeaderHeight = 28;
        private const int WindowHeightWithLog = 938;
        private const int WindowHeightWithoutLog = 870;
        private const int DefaultClientWidth = 1024;
        private const int ShellBottomReserve = 130;

        private sealed class TimedResult<T>
        {
            public bool TimedOut;
            public T Value;
        }

        private sealed class PreflightResult
        {
            public bool Passed;
            public string Failure;
        }

        private sealed class UiPreferences
        {
            public bool LogVisible = true;
            public bool HasBounds;
            public Rectangle Bounds;
            public bool Maximized;
            public int Tab;
        }

        private readonly PS4Connection connection = new PS4Connection();
        private readonly string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        private GameProfile profile;
        private StatEngine engine;
        private int gamePid = -1;
        private bool gameReady;
        private bool busy;
        private string operationTitle = string.Empty;
        private readonly bool previewMode;
        private CancellationTokenSource cancellation;
        private int lastProgressTick;

        private TextBox ipTextBox, payloadPortTextBox, ftpPortTextBox, logTextBox;
        private Button connectPs4Button, connectGameButton, cancelButton;
        private Label firmwareLabel, titleIdLabel, patchLabel, preflightLabel, progressLabel, lockLabel;
        private Panel connectionGroup, characterGroup;
        private Control topHost;
        private Panel toolbar;
        private Panel lockOverlay;
        private List<LinkLabel> creditLinks;
        private Label creditAuthor;
        private RadioButton character1Button, character2Button;
        private CheckBox saveCheckBox, verifyCheckBox, confirmCheckBox, showLogCheckBox;
        private ThemedTabs tabs;
        private OutfitsPanel outfitsPanel;
        private DebugPanel debugPanel;
        private ToolsPanel toolsPanel;
        private ProgressStrip progressBar;
        private ToolStripStatusLabel statusLabel;

        /// <param name="preview">Débloque toute la fenêtre sans console, pour la regarder; rien n'est écrit.</param>
        public MainForm(bool preview = false)
        {
            previewMode = preview;
            BuildUi();
            LoadConnectionInfo();
            InitializeEngine();
            Theme.Apply(this);
            UpdateGate();
            ApplyStartupBounds();
            Shown += (s, e) => LayoutShell();

            if (previewMode)
            {
                Text += " - aperçu";
                Log("Mode aperçu : la fenêtre est débloquée sans console. Rien ne peut être envoyé au jeu.");
                SetStatus("Mode aperçu - aucune console connectée", Color.DarkOrange);
            }
        }

        // ---- IToolHost ------------------------------------------------------------------

        StatEngine IToolHost.Engine => engine;

        PS4Connection IToolHost.Connection => connection;

        int IToolHost.Slot => character1Button.Checked ? 0 : (character2Button.Checked ? 1 : -1);

        bool IToolHost.IsBusy => busy;

        void IToolHost.ChooseCharacter(int slot)
        {
            if (slot == 0)
                character1Button.Checked = true;
            else if (slot == 1)
                character2Button.Checked = true;
        }

        void IToolHost.Log(string message)
        {
            Log(message);
        }

        void IToolHost.SetStatus(string text, Color color)
        {
            SetStatus(text, color);
        }

        bool IToolHost.Confirm(string message, string caption)
        {
            return MessageBox.Show(message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        public Task RunOptionAsync(IRecoveryOption option)
        {
            return RunAsync(option.Name, option.RequiredHandlers, option.OptionalHandlers, option.Run);
        }

        public async Task RunAsync(string title, IEnumerable<string> requiredHandlers, IEnumerable<string> optionalHandlers,
            Func<OptionContext, OptionResult> work, string confirmDetails = null, bool changesStats = true, bool needsCharacter = true)
        {
            IToolHost host = this;
            if (busy || !gameReady)
            {
                string reason = busy ? "une autre opération est en cours"
                    : previewMode ? "mode aperçu : aucune console connectée"
                    : "le jeu n'est pas connecté (connectez-vous à la PS4 puis à GTA V)";
                Log($"« {title} » non démarrée : {reason}.");
                SetStatus($"« {title} » non démarrée", Color.DarkOrange);
                return;
            }

            if (needsCharacter && host.Slot < 0)
            {
                MessageBox.Show("Choisissez Personnage 1 ou Personnage 2 dans la barre d'outils d'abord.", "Personnage", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> required = requiredHandlers.ToList();
            string missing = required.FirstOrDefault(handler => !engine.IsAvailable(handler));
            if (missing != null)
            {
                Log($"« {title} » a besoin du handler « {missing} », indisponible sur ce build.");
                SetStatus($"« {title} » indisponible sur ce build", Color.DarkOrange);
                return;
            }

            string who = character1Button.Checked ? "Personnage 1" : "Personnage 2";
            bool askToApply = confirmDetails != null || (changesStats && confirmCheckBox.Checked);
            List<string> unverified = required.Concat(optionalHandlers)
                .Distinct()
                .Where(handler => engine.IsAvailable(handler) && !engine.IsVerified(handler))
                .ToList();

            if (askToApply || unverified.Count > 0)
            {
                string where = host.Slot >= 0 ? " sur " + who : string.Empty;
                StringBuilder message = new StringBuilder(askToApply
                    ? $"{(changesStats ? "Appliquer" : "Exécuter")} « {title} »{where} ?\n\n{confirmDetails ?? "Cette action modifie vos stats."}"
                    : $"Exécuter « {title} »{where} ?");

                if (unverified.Count > 0)
                    message.Append("\n\nPremière exécution sur ce build : le jeu n'a pas encore été vérifié avec " + string.Join(", ", unverified) +
                                   ". Si quelque chose cloche, le jeu peut planter.");

                if (!host.Confirm(message.ToString(), "Confirmation"))
                    return;
            }

            BeginOperation(title);
            try
            {
                int currentPid = await Task.Run(() => connection.FindProcessID(GameProcessName));
                if (currentPid != gamePid)
                {
                    Log("Le process GTA V a changé ou s'est fermé. Reconnectez-vous au jeu.");
                    ResetGameState();
                    SetStatus("Jeu redémarré - reconnectez GTA V", Color.DarkOrange);
                    return;
                }

                OptionContext context = new OptionContext
                {
                    Engine = engine,
                    Slot = host.Slot,
                    Verify = verifyCheckBox.Checked,
                    Save = saveCheckBox.Checked,
                    Log = Log,
                    Progress = ReportProgress,
                    Cancel = cancellation.Token
                };

                Log(context.Slot >= 0 ? $"Exécution « {title} » sur MP{context.Slot}, vérif={context.Verify}, enregistrement={context.Save}." : $"Exécution « {title} ».");
                OptionResult result = await Task.Run(() => work(context));

                Log($"« {title} » terminé : {result.Summary}.");
                if (result.IsClean)
                    await Task.Run(() => engine.CommitSignatures());

                SetStatus(result.IsClean ? $"« {title} » appliqué" : $"« {title} » terminé : {result.Summary}",
                    result.IsClean ? Color.Green : Color.DarkOrange);

                if (result.IsClean && changesStats)
                    try { connection.Notify($"Arise - RTM: {title}"); } catch { }
            }
            catch (OperationCanceledException)
            {
                Log($"« {title} » annulé. Ce qui est déjà écrit reste écrit.");
                SetStatus($"« {title} » annulé", Color.DarkOrange);
            }
            catch (Exception ex)
            {
                Log("ERREUR : " + ex.Message);
                if (IsProtocolFailure(ex))
                {
                    ResetSession("Connexion PS4 désynchronisée - reconnectez la PS4", Color.Red);
                    return;
                }

                SetStatus($"« {title} » a échoué", Color.Red);
            }
            finally
            {
                EndOperation();
            }
        }

        // ---- verrou d'opération ---------------------------------------------------------

        private void BeginOperation(string title)
        {
            busy = true;
            operationTitle = title;
            cancellation = new CancellationTokenSource();
            progressLabel.Text = title;
            progressBar.Indeterminate = true;
            cancelButton.Enabled = true;
            UpdateGate();
        }

        private void EndOperation()
        {
            busy = false;
            progressBar.Indeterminate = false;
            progressBar.Value = 0;
            progressLabel.Text = "Inactif";
            cancelButton.Enabled = false;
            cancellation?.Dispose();
            cancellation = null;
            UpdateGate();
        }

        private void ReportProgress(int done, int total)
        {
            int now = Environment.TickCount;
            if (done < total && now - lastProgressTick < 80)
                return;

            lastProgressTick = now;
            if (IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (!busy)
                    return;

                progressBar.Indeterminate = false;
                progressBar.Maximum = Math.Max(1, total);
                progressBar.Value = Math.Min(done, progressBar.Maximum);
                progressBar.Invalidate();
                progressLabel.Text = $"{operationTitle}  {done:N0}/{total:N0}";
            }));
        }

        private void UpdateGate()
        {
            connectionGroup.Enabled = !busy;
            bool usable = gameReady || previewMode;
            characterGroup.Enabled = usable && !busy;
            lockOverlay.Visible = !usable || busy;

            if (busy)
                lockLabel.Text = "En cours : " + operationTitle;
            else
                lockLabel.Text = "Connectez-vous d'abord à la PS4 puis à GTA V.";

            Theme.StyleToggle(character1Button);
            Theme.StyleToggle(character2Button);
            outfitsPanel?.SetCharacterChosen(((IToolHost)this).Slot >= 0);
            debugPanel?.SetCharacter(((IToolHost)this).Slot);
            toolsPanel?.SetCharacterChosen(((IToolHost)this).Slot >= 0);
        }

        // ---- UI -------------------------------------------------------------------------

        private void BuildUi()
        {
            Text = "Arise - RTM (GTA V 1.57) - FR";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            bool showLog = LoadLogPreference();
            ClientSize = new Size(DefaultClientWidth, showLog ? WindowHeightWithLog : WindowHeightWithoutLog);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;

            topHost = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = Theme.Bg, Tag = "keep" };

            Panel banner = new Panel { Location = new Point(0, 0), Size = new Size(DefaultClientWidth, 36), BackColor = Color.FromArgb(8, 8, 8), Tag = "keep" };
            banner.Paint += (s, e) =>
            {
                using (Brush accent = new SolidBrush(Theme.Yellow))
                    e.Graphics.FillRectangle(accent, 0, banner.Height - 2, banner.Width, 2);
            };
            banner.Controls.Add(new Label
            {
                Text = "Arise - RTM", AutoSize = true, Location = new Point(10, 8),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Theme.Text
            });
            firmwareLabel = BannerInfo("Firmware : N/D", 200);
            titleIdLabel = BannerInfo("Title ID : N/D", 320);
            patchLabel = BannerInfo("Version : N/D", 480);
            preflightLabel = BannerInfo("Précontrôle : non lancé", 570);
            banner.Controls.AddRange(new Control[] { firmwareLabel, titleIdLabel, patchLabel, preflightLabel });
            AddCredits(banner);
            topHost.Controls.Add(banner);

            toolbar = new Panel { Location = new Point(0, 36), Height = 70, BackColor = Theme.Bg };
            toolbar.Paint += (s, e) =>
            {
                using (Pen line = new Pen(Theme.Line))
                    e.Graphics.DrawLine(line, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            connectionGroup = new Panel { Location = new Point(8, 4), Height = 28, BackColor = Theme.Bg };
            connectionGroup.Controls.Add(NewLabel("IP PS4", 0, 0));
            ipTextBox = new TextBox { Size = new Size(120, 21) };
            connectionGroup.Controls.Add(ipTextBox);
            connectionGroup.Controls.Add(NewLabel("Payload", 0, 0));
            payloadPortTextBox = new TextBox { Size = new Size(46, 21), Text = "9090" };
            connectionGroup.Controls.Add(payloadPortTextBox);
            connectionGroup.Controls.Add(NewLabel("FTP", 0, 0));
            ftpPortTextBox = new TextBox { Size = new Size(46, 21), Text = "2121" };
            connectionGroup.Controls.Add(ftpPortTextBox);

            connectPs4Button = new TileButton { Text = "1. CONNECTER PS4", Size = new Size(180, 24), Primary = true, Centered = true };
            connectPs4Button.Click += ConnectPs4Button_Click;
            connectGameButton = new TileButton { Text = "2. CONNECTER GTA V", Size = new Size(180, 24), Primary = true, Centered = true, Enabled = false };
            connectGameButton.Click += ConnectGameButton_Click;
            connectionGroup.Controls.AddRange(new Control[] { connectPs4Button, connectGameButton });

            showLogCheckBox = new CheckBox { Text = "Afficher le journal", Size = new Size(130, 20), Checked = showLog };
            showLogCheckBox.CheckedChanged += (s, e) => ApplyLogVisibility();
            connectionGroup.Controls.Add(showLogCheckBox);

            characterGroup = new Panel { Location = new Point(8, 36), Height = 28, BackColor = Theme.Bg, Enabled = false };
            characterGroup.Controls.Add(NewLabel("Personnage", 0, 0));
            Panel characterPanel = new Panel { Size = new Size(244, 26), BackColor = Theme.Bg };
            character1Button = Toggle("Personnage 1", 0, 0, 120);
            character2Button = Toggle("Personnage 2", 124, 0, 120);
            characterPanel.Controls.AddRange(new Control[] { character1Button, character2Button });
            characterGroup.Controls.Add(characterPanel);

            saveCheckBox = new CheckBox { Text = "Enregistrer après écriture", Size = new Size(170, 20), Checked = true };
            verifyCheckBox = new CheckBox { Text = "Vérifier après écriture (lent)", Size = new Size(200, 20) };
            confirmCheckBox = new CheckBox { Text = "Demander avant d'appliquer", Size = new Size(180, 20), Checked = true };
            characterGroup.Controls.AddRange(new Control[] { saveCheckBox, verifyCheckBox, confirmCheckBox });

            toolbar.Controls.Add(connectionGroup);
            toolbar.Controls.Add(characterGroup);
            topHost.Controls.Add(toolbar);
            Controls.Add(topHost);

            tabs = new ThemedTabs
            {
                Location = new Point(8, 114),
                Size = new Size(984, PanelHeight + TabHeaderHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
            };
            outfitsPanel = new OutfitsPanel(this);
            toolsPanel = new ToolsPanel(this);
            debugPanel = new DebugPanel(this);
            tabs.AddPage(outfitsPanel);
            tabs.AddPage(toolsPanel);
            tabs.AddPage(debugPanel);
            Controls.Add(tabs);

            lockLabel = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold), BackColor = Theme.Bg
            };
            lockOverlay = new Panel
            {
                Location = new Point(8, 114 + TabHeaderHeight),
                Size = new Size(984, PanelHeight),
                BackColor = Theme.Bg,
                Tag = "keep",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right
            };
            lockOverlay.Controls.Add(lockLabel);
            Controls.Add(lockOverlay);
            lockOverlay.BringToFront();

            progressLabel = new Label
            {
                Text = "Inactif", Location = new Point(8, 814), Size = new Size(984, 14), ForeColor = Theme.Muted,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            progressBar = new ProgressStrip
            {
                Location = new Point(8, 832), Size = new Size(862, 6),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            cancelButton = new TileButton
            {
                Text = "Annuler", Location = new Point(878, 823), Size = new Size(114, 22), Centered = true, Enabled = false,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            cancelButton.Click += (s, e) => cancellation?.Cancel();
            Controls.AddRange(new Control[] { progressLabel, progressBar, cancelButton });

            logTextBox = new TextBox
            {
                Location = new Point(8, 852), Size = new Size(984, 62),
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = Theme.Mono, BackColor = Color.FromArgb(11, 11, 11), ForeColor = Color.FromArgb(190, 190, 190),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(logTextBox);
            ApplyLogVisibility(false, false);

            StatusStrip strip = new StatusStrip { BackColor = Theme.Bg };
            statusLabel = new ToolStripStatusLabel("STATUT : Inactif") { ForeColor = Theme.Text };
            strip.Items.Add(statusLabel);
            Controls.Add(strip);

            MinimumSize = new Size(760, 560);
            outfitsPanel.AutoScroll = true;
            toolsPanel.AutoScroll = true;
            debugPanel.AutoScroll = true;

            Resize += (s, e) => { LayoutToolbar(); LayoutShell(); };
            topHost.SizeChanged += (s, e) => LayoutShell();
            LayoutToolbar();
            LayoutShell();

            FormClosing += (sender, args) =>
            {
                SaveUiPreferences();
                cancellation?.Cancel();
                try { connection.Disconnect(); } catch { }
            };
        }

        /// <summary>Place les contrôles de connexion et d'options ligne par ligne. Dès que la
        /// largeur manque, la ligne suivante passe à la ligne et la barre d'outils grandit.</summary>
        private void LayoutToolbar()
        {
            if (toolbar == null || topHost == null || connectionGroup == null || characterGroup == null)
                return;

            int width = topHost.ClientSize.Width;
            if (width <= 0)
                return;

            if (topHost.Controls.Count > 0)
                topHost.Controls[0].Width = width;
            toolbar.Width = width;

            const int gap = 10;
            const int rowHeight = 30;
            int contentWidth = Math.Max(0, width - 16);

            int connectionHeight = LayoutFlow(connectionGroup, contentWidth, rowHeight, gap);
            connectionGroup.Location = new Point(8, 4);

            int characterHeight = LayoutFlow(characterGroup, contentWidth, rowHeight, gap);
            characterGroup.Location = new Point(8, 4 + connectionHeight + 4);

            toolbar.Height = 4 + connectionHeight + 4 + characterHeight + 4;
            topHost.Height = 36 + toolbar.Height;
        }

        private static int LayoutFlow(Panel host, int width, int rowHeight, int gap)
        {
            host.Width = width;
            int x = 0;
            int row = 0;

            foreach (Control child in host.Controls)
            {
                if (x > 0 && x + child.Width > width)
                {
                    x = 0;
                    row++;
                }

                child.Location = new Point(x, row * rowHeight + Math.Max(0, (rowHeight - child.Height) / 2));
                x += child.Width + gap;
            }

            host.Height = (row + 1) * rowHeight;
            return host.Height;
        }

        /// <summary>Aligne l'onglet et son voile sur le bas réel de la barre d'outils, qui peut
        /// grandir quand les lignes de connexion/options passent à la ligne.</summary>
        private void LayoutShell()
        {
            if (topHost == null || tabs == null || lockOverlay == null)
                return;

            int top = topHost.Bottom;
            int width = Math.Max(0, ClientSize.Width - 16);
            int height = Math.Max(0, ClientSize.Height - ShellBottomReserve - top);
            tabs.SetBounds(8, top, width, height);
            lockOverlay.SetBounds(8, top + TabHeaderHeight, width, Math.Max(0, height - TabHeaderHeight));
        }

        /// <summary>Liens cliquables + mention d'auteur, positionnés à droite du bandeau et
        /// repositionnés à chaque redimensionnement de la fenêtre. 4 liens seulement :
        /// les autres (YouTube Z, Twitch Z, Discord Z) sont dans CREDITS.txt.</summary>
        private void AddCredits(Panel banner)
        {
            const int gap = 10;
            const int rightPad = 10;

            (string Text, Action Open)[] items =
            {
                ("Crédits",  OpenCreditsFile),
                ("Discord",  () => LinkOpener.Open(DiscordUrl, Log)),
                ("X",        () => LinkOpener.Open(XUrl, Log)),
                ("Site",     () => LinkOpener.Open(ZitouneSiteUrl, Log))
            };

            List<LinkLabel> links = new List<LinkLabel>(items.Length);
            foreach ((string text, Action open) in items)
            {
                LinkLabel link = new LinkLabel
                {
                    Text = text, AutoSize = true, LinkColor = Theme.Yellow, ActiveLinkColor = Theme.YellowHover,
                    VisitedLinkColor = Theme.Yellow, LinkBehavior = LinkBehavior.HoverUnderline,
                    BackColor = banner.BackColor, Cursor = Cursors.Hand,
                    Location = new Point(0, 11)
                };
                link.LinkClicked += (s, e) => open();
                banner.Controls.Add(link);
                links.Add(link);
            }

            Label author = new Label
            {
                Text = "yAmethxst / Zitoune",
                AutoSize = true, ForeColor = Theme.Text, BackColor = banner.BackColor,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Location = new Point(0, 11)
            };
            banner.Controls.Add(author);
            creditLinks = links;
            creditAuthor = author;

            // Repositionne tous les crédits à droite, de droite à gauche, chaque fois que
            // le bandeau change de largeur (donc chaque Resize de la fenêtre). Les éléments
            // qui toucheraient les infos de gauche sont masqués plutôt que superposés.
            Action layout = () =>
            {
                int minLeft = (preflightLabel != null && preflightLabel.Visible ? preflightLabel.Right : 0) + 8;
                int x = banner.ClientSize.Width - rightPad;
                for (int i = links.Count - 1; i >= 0; i--)
                {
                    int left = x - links[i].PreferredWidth;
                    links[i].Visible = left >= minLeft;
                    if (links[i].Visible)
                    {
                        links[i].Left = left;
                        x = left - gap;
                    }
                }

                int authorLeft = x - author.PreferredWidth;
                author.Visible = authorLeft >= minLeft;
                author.Left = authorLeft;
            };
            banner.Resize += (s, e) => layout();
            banner.HandleCreated += (s, e) => layout();
            layout();
        }

        private void OpenCreditsFile()
        {
            string path = Path.Combine(baseDirectory, "CREDITS.txt");
            if (!File.Exists(path))
            {
                MessageBox.Show("CREDITS.txt est introuvable à côté de l'exécutable.", "Crédits", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LinkOpener.Open(path, Log);
        }

        private RadioButton Toggle(string text, int x, int y, int width)
        {
            RadioButton button = new RadioButton { Text = text, Location = new Point(x, y), Size = new Size(width, 24) };
            button.CheckedChanged += (s, e) => UpdateGate();
            return button;
        }

        private static Label NewLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Theme.Muted };
        }

        private static Label BannerInfo(string text, int x)
        {
            return new Label { Text = text, Location = new Point(x, 11), AutoSize = true, ForeColor = Theme.Muted };
        }

        private static Color StatusColor(Color requested)
        {
            if (requested == Color.Green) return Theme.Ok;
            if (requested == Color.Red) return Theme.Bad;
            if (requested == Color.DarkOrange) return Theme.Yellow;
            return Theme.Text;
        }

        private void InitializeEngine()
        {
            try
            {
                profile = GameProfile.Load(Path.Combine(baseDirectory, "profile.ini"), TargetGameVersion);
                engine = new StatEngine(connection, profile, new SignatureStore(Path.Combine(baseDirectory, "signatures.ini")), Log);

                bool hashOk = JenkinsHash.Joaat("adder") == 0xB779A091;
                Log(hashOk ? "Auto-test hash OK." : "Auto-test hash ÉCHEC.");
                if (!hashOk)
                    throw new InvalidOperationException("Auto-test hash échoué.");

                Log($"Profil {profile.GameVersion} chargé (base 0x{profile.AddressBase:X}, {profile.Handlers.Count} handler(s)).");

                try
                {
                    OutfitOffsets.Load(TargetGameVersion);
                }
                catch (Exception ex)
                {
                    outfitsPanel.Enabled = false;
                    Log("Tenues désactivées : " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                connectPs4Button.Enabled = false;
                SetStatus("Erreur de configuration", Color.Red);
                MessageBox.Show($"Erreur de configuration :\n{ex.Message}", "Arise - RTM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- connexion ------------------------------------------------------------------

        private async void ConnectPs4Button_Click(object sender, EventArgs e)
        {
            if (connection.IsConnected)
            {
                ResetSession("Déconnecté.");
                return;
            }

            if (TryReadPorts(out int payloadPort, out int ftpPort))
                await ConnectToPs4Async(payloadPort);
        }

        private async void ConnectGameButton_Click(object sender, EventArgs e)
        {
            if (TryReadPorts(out int payloadPort, out int ftpPort))
                await ConnectToGameAsync(payloadPort, ftpPort);
        }

        private async Task ConnectToPs4Async(int payloadPort)
        {
            string ip = ipTextBox.Text.Trim();
            if (ip.Length == 0)
            {
                MessageBox.Show("Entrez l'adresse IP de la PS4.", "Saisie manquante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetConnectionBusy(true, "VÉRIFICATION...");
            try
            {
                TimedResult<bool> debugCheck = await RunWithTimeoutAsync(() => connection.IsDebugActive(ip), ConnectionTimeoutMs);
                if (debugCheck.TimedOut)
                {
                    FailPs4("Timeout vérification debug");
                    return;
                }

                if (!debugCheck.Value)
                {
                    connectPs4Button.Text = "INJECTION...";
                    string payloadPath = Path.Combine(baseDirectory, "ps4debug.bin");
                    TimedResult<bool> payload = await RunWithTimeoutAsync(
                        () => connection.InjectPayload(ip, payloadPath, payloadPort), ConnectionTimeoutMs);

                    if (payload.TimedOut || !payload.Value)
                    {
                        MessageBox.Show("Activez « PayLoader Server » dans les paramètres GoldHEN (port 9090).",
                            "Payload indisponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        FailPs4("Payload injoignable");
                        return;
                    }

                    await Task.Delay(1200);
                }

                connectPs4Button.Text = "CONNEXION...";
                TimedResult<bool> connect = await RunWithTimeoutAsync(() =>
                {
                    connection.Connect(ip);
                    return connection.IsConnected;
                }, ConnectionTimeoutMs);

                if (connect.TimedOut || !connect.Value)
                {
                    FailPs4("Timeout connexion PS4");
                    return;
                }

                connection.Notify("Arise - RTM : Connecté à la PS4 !");
                firmwareLabel.Text = $"FW : {connection.GetConsoleFirmwareText()}";
                SetStatus("Connecté à la PS4 - connectez maintenant GTA V", Color.Green);
                Log($"Connecté à {ip}.");
            }
            catch (Exception ex)
            {
                FailPs4("Erreur de connexion PS4");
                Log("ERREUR : " + ex.Message);
            }
            finally
            {
                SetConnectionBusy(false);
            }
        }

        private async Task ConnectToGameAsync(int payloadPort, int ftpPort)
        {
            string ip = ipTextBox.Text.Trim();
            SetConnectionBusy(true, null, "CONNEXION GTA V...");
            try
            {
                int pid = await Task.Run(() => connection.FindProcessID(GameProcessName));
                if (pid <= 0)
                {
                    SetStatus("GTA V introuvable - démarrez le jeu", Color.DarkOrange);
                    MessageBox.Show("eboot.bin introuvable. Démarrez le jeu.", "Introuvable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string titleId = "N/D";
                if (connection.TryGetProcessInfo(pid, out ProcessInfo processInfo))
                    titleId = CleanValue(processInfo.titleid);

                titleIdLabel.Text = $"Title ID : {titleId} ({RegionOf(titleId)})";
                if (!profile.AllowedTitleIds.Contains(titleId))
                {
                    FailGame($"Title ID {titleId} non supporté par cet outil.");
                    return;
                }

                string patch = await DetectPatchAsync(ip, titleId, ftpPort);
                if (patch.Length == 0)
                {
                    FailGame("Version du jeu non confirmée.");
                    return;
                }

                patchLabel.Text = $"Version : {patch}";
                if (patch != profile.GameVersion)
                {
                    FailGame($"Version {patch} non supportée (attendu {profile.GameVersion}).");
                    return;
                }

                preflightLabel.Text = "Précontrôle : en cours...";
                PreflightResult preflight = await Task.Run(() => RunPreflight(pid, titleId));
                if (!preflight.Passed)
                {
                    preflightLabel.Text = "Précontrôle : ÉCHEC";
                    Log("ÉCHEC DU PRÉCONTRÔLE : " + preflight.Failure);
                    MessageBox.Show(preflight.Failure, "Précontrôle échoué", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    FailGame("Précontrôle échoué");
                    return;
                }

                gamePid = pid;
                gameReady = true;
                preflightLabel.Text = "Précontrôle : OK";
                SaveConnectionInfo(ip, payloadPort, ftpPort);
                outfitsPanel?.Attach(pid);

                try { connection.Notify("Arise - RTM : GTA V connecté"); } catch { }

                bool coreVerified = HandlerKeys.Core.All(engine.IsVerified);
                string verified = coreVerified ? "cœur vérifié" : "cœur NON VÉRIFIÉ (première exécution sur ce build)";
                SetStatus($"GTA V trouvé (PID {pid}) - {verified}.", coreVerified ? Color.Green : Color.DarkOrange);
                Log($"GTA V {patch} {titleId} PID {pid} : précontrôle réussi, {verified}.");
            }
            catch (Exception ex)
            {
                Log("ERREUR : " + ex.Message);
                if (IsProtocolFailure(ex))
                    ResetSession("Connexion PS4 désynchronisée - reconnectez la PS4", Color.Red);
                else
                    FailGame("Erreur de détection GTA V");
            }
            finally
            {
                SetConnectionBusy(false);
                UpdateGate();
            }
        }

        private async Task<string> DetectPatchAsync(string ip, string titleId, int ftpPort)
        {
            TimedResult<string> ftp = await RunWithTimeoutAsync(() =>
                connection.TryGetGamePatchVersionFromFtp(ip, titleId, out string appVersion, out _, out _, out _, ftpPort)
                    ? appVersion
                    : string.Empty,
                ConnectionTimeoutMs);

            string patch = ftp.TimedOut ? string.Empty : NormalizeVersion(ftp.Value);
            if (patch.Length > 0)
            {
                Log($"Version du jeu lue depuis param.sfo : {patch}.");
                return patch;
            }

            DialogResult answer = MessageBox.Show(
                $"La version du jeu n'a pas pu être lue via FTP.\nCette partie est-elle GTA V {profile.GameVersion} ?",
                "Version du jeu", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
                return string.Empty;

            Log($"Version du jeu confirmée manuellement : {profile.GameVersion}.");
            return profile.GameVersion;
        }

        private PreflightResult RunPreflight(int pid, string titleId)
        {
            try
            {
                bool passed = engine.Preflight(pid, titleId, out string failure);
                return new PreflightResult { Passed = passed, Failure = failure };
            }
            catch (Exception ex)
            {
                return new PreflightResult { Passed = false, Failure = ex.Message };
            }
        }

        private void FailPs4(string status)
        {
            ResetSession(status, Color.Red);
        }

        private void FailGame(string status)
        {
            ResetGameState();
            SetStatus(status, Color.Red);
            Log(status);
        }

        private void ResetSession(string status, Color? color = null)
        {
            try { connection.Disconnect(); } catch { }
            ResetGameState();
            firmwareLabel.Text = "FW : N/D";
            SetStatus(status, color ?? Color.Black);
            Log(status);
            SetConnectionBusy(false);
        }

        private void ResetGameState()
        {
            engine?.Reset();
            gameReady = false;
            gamePid = -1;
            outfitsPanel?.Detach();
            titleIdLabel.Text = "Title ID : N/D";
            patchLabel.Text = "Version : N/D";
            preflightLabel.Text = "Précontrôle : non lancé";
            UpdateGate();
        }

        private void SetConnectionBusy(bool working, string ps4Text = null, string gameText = null)
        {
            connectPs4Button.Enabled = !working;
            connectGameButton.Enabled = !working && connection.IsConnected;

            if (ps4Text != null)
                connectPs4Button.Text = ps4Text;
            if (gameText != null)
                connectGameButton.Text = gameText;

            if (!working)
            {
                connectPs4Button.Text = connection.IsConnected ? "DÉCONNECTER PS4" : "1. CONNECTER PS4";
                connectGameButton.Text = gameReady ? "GTA V CONNECTÉ (reconnecter)" : "2. CONNECTER GTA V";
                connectGameButton.Enabled = connection.IsConnected;
            }
        }

        // ---- helpers --------------------------------------------------------------------

        private void SetStatus(string text, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetStatus(text, color)));
                return;
            }

            if (statusLabel == null)
                return;

            statusLabel.Text = "STATUT : " + text;
            statusLabel.ForeColor = StatusColor(color);
        }

        private void Log(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), message);
                return;
            }

            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (logTextBox != null)
                logTextBox.AppendText(line + Environment.NewLine);

            try { File.AppendAllText(Path.Combine(baseDirectory, "rtm.log"), line + Environment.NewLine); }
            catch { }
        }

        private bool TryReadPorts(out int payloadPort, out int ftpPort)
        {
            ftpPort = 2121;
            if (!int.TryParse(payloadPortTextBox.Text.Trim(), out payloadPort) || payloadPort < 1 || payloadPort > 65535)
            {
                MessageBox.Show("Port payload invalide.", "Saisie invalide", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!int.TryParse(ftpPortTextBox.Text.Trim(), out ftpPort) || ftpPort < 1 || ftpPort > 65535)
            {
                MessageBox.Show("Port FTP invalide.", "Saisie invalide", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private string UiPreferencePath => Path.Combine(baseDirectory, "ui.txt");

        private bool LoadLogPreference()
        {
            return LoadUiPreferences().LogVisible;
        }

        private UiPreferences LoadUiPreferences()
        {
            UiPreferences prefs = new UiPreferences();
            try
            {
                if (!File.Exists(UiPreferencePath))
                    return prefs;

                foreach (string raw in File.ReadAllLines(UiPreferencePath))
                {
                    string line = raw.Trim();
                    int separator = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || separator <= 0)
                        continue;

                    string key = line.Substring(0, separator).Trim().ToLowerInvariant();
                    string value = line.Substring(separator + 1).Trim();

                    switch (key)
                    {
                        case "log": prefs.LogVisible = value != "0"; break;
                        case "max": prefs.Maximized = value == "1"; break;
                        case "tab": if (int.TryParse(value, out int tab)) prefs.Tab = tab; break;
                        case "x": if (int.TryParse(value, out int x)) { prefs.Bounds.X = x; prefs.HasBounds = true; } break;
                        case "y": if (int.TryParse(value, out int y)) { prefs.Bounds.Y = y; prefs.HasBounds = true; } break;
                        case "w": if (int.TryParse(value, out int w)) { prefs.Bounds.Width = w; prefs.HasBounds = true; } break;
                        case "h": if (int.TryParse(value, out int h)) { prefs.Bounds.Height = h; prefs.HasBounds = true; } break;
                    }
                }
            }
            catch
            {
            }

            if (prefs.Bounds.Width < 320 || prefs.Bounds.Height < 240)
                prefs.HasBounds = false;
            return prefs;
        }

        private void SaveUiPreferences()
        {
            try
            {
                Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    bounds = Bounds;

                StringBuilder text = new StringBuilder();
                text.AppendLine("log=" + (showLogCheckBox != null && showLogCheckBox.Checked ? "1" : "0"));
                text.AppendLine("max=" + (WindowState == FormWindowState.Maximized ? "1" : "0"));
                text.AppendLine("tab=" + (tabs != null ? tabs.SelectedIndex : 0));
                text.AppendLine("x=" + bounds.X);
                text.AppendLine("y=" + bounds.Y);
                text.AppendLine("w=" + bounds.Width);
                text.AppendLine("h=" + bounds.Height);
                File.WriteAllText(UiPreferencePath, text.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        /// <summary>Restaure les bounds si l'écran existe toujours, sinon centre une fenêtre
        /// qui tient dans la zone de travail (le format 1366x768 est le cas courant).</summary>
        private void ApplyStartupBounds()
        {
            UiPreferences prefs = LoadUiPreferences();
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int maxWidth = Math.Max(320, work.Width - 16);
            int maxHeight = Math.Max(240, work.Height - 16);
            int minWidth = Math.Min(MinimumSize.Width, maxWidth);
            int minHeight = Math.Min(MinimumSize.Height, maxHeight);

            Rectangle bounds;
            if (prefs.HasBounds && Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(prefs.Bounds)))
                bounds = prefs.Bounds;
            else
                bounds = new Rectangle(0, 0, Math.Max(Width, MinimumSize.Width), Math.Max(Height, MinimumSize.Height));

            bounds.Width = Math.Max(minWidth, Math.Min(bounds.Width, maxWidth));
            bounds.Height = Math.Max(minHeight, Math.Min(bounds.Height, maxHeight));

            if (prefs.HasBounds)
            {
                if (bounds.Right > work.Right) bounds.X = work.Right - bounds.Width;
                if (bounds.Bottom > work.Bottom) bounds.Y = work.Bottom - bounds.Height;
                if (bounds.Left < work.Left) bounds.X = work.Left;
                if (bounds.Top < work.Top) bounds.Y = work.Top;
            }
            else
            {
                bounds.X = work.Left + (work.Width - bounds.Width) / 2;
                bounds.Y = work.Top + (work.Height - bounds.Height) / 2;
            }

            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            if (prefs.Maximized)
                WindowState = FormWindowState.Maximized;
            if (prefs.Tab >= 0 && prefs.Tab <= 2)
                tabs.SelectedIndex = prefs.Tab;
            LayoutShell();
        }

        private void SetClientHeight(int desired)
        {
            int nonClient = Math.Max(0, Height - ClientSize.Height);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int minClient = Math.Max(200, MinimumSize.Height - nonClient);
            int maxClient = Math.Max(minClient, work.Height - nonClient - 8);
            int height = Math.Max(minClient, Math.Min(maxClient, desired));
            ClientSize = new Size(ClientSize.Width, height);
        }

        /// <summary>Cache ou affiche le panneau de log sans écraser la hauteur choisie :
        /// on applique seulement le delta, borné à la zone de travail.</summary>
        private void ApplyLogVisibility(bool save = true, bool adjustHeight = true)
        {
            if (showLogCheckBox == null || logTextBox == null)
                return;

            bool show = showLogCheckBox.Checked;
            logTextBox.Visible = show;

            if (adjustHeight && WindowState == FormWindowState.Normal)
            {
                int delta = WindowHeightWithLog - WindowHeightWithoutLog;
                SetClientHeight(ClientSize.Height + (show ? delta : -delta));
            }

            LayoutShell();
            if (save)
                SaveUiPreferences();
        }

        private void LoadConnectionInfo()
        {
            try
            {
                string path = Path.Combine(baseDirectory, "connection.txt");
                if (!File.Exists(path))
                    return;

                string[] parts = File.ReadAllText(path).Trim().Split(':');
                if (parts.Length >= 1) ipTextBox.Text = parts[0];
                if (parts.Length >= 2) payloadPortTextBox.Text = parts[1];
                if (parts.Length >= 3) ftpPortTextBox.Text = parts[2];
            }
            catch
            {
            }
        }

        private void SaveConnectionInfo(string ip, int payloadPort, int ftpPort)
        {
            try
            {
                File.WriteAllText(Path.Combine(baseDirectory, "connection.txt"), $"{ip}:{payloadPort}:{ftpPort}", Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static async Task<TimedResult<T>> RunWithTimeoutAsync<T>(Func<T> operation, int timeoutMs)
        {
            Task<T> worker = Task.Run(operation);
            Task completed = await Task.WhenAny(worker, Task.Delay(timeoutMs));

            if (completed == worker)
                return new TimedResult<T> { TimedOut = false, Value = await worker };

            return new TimedResult<T> { TimedOut = true, Value = default(T) };
        }

        private static string CleanValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "N/D" : value.Trim().TrimEnd('\0');
        }

        private static string NormalizeVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string[] parts = raw.Trim().Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                return $"{major}.{minor:00}";

            return raw.Trim();
        }

        /// <summary>Erreur qui laisse le flux ps4debug désynchronisé : la session doit être recréée.</summary>
        private static bool IsProtocolFailure(Exception ex)
        {
            string message = ex?.Message ?? string.Empty;
            return message.IndexOf("désynchron", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("ps4debug", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("libdbg status", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string RegionOf(string titleId)
        {
            switch (titleId.ToUpperInvariant())
            {
                case "CUSA00419": return "US";
                case "CUSA00411": return "EU";
                case "CUSA00880": return "JP";
                default: return "INCONNU";
            }
        }
    }
}
