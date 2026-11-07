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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>Onglet principal : éditeur de tenues (noms, composants, import texte/fichier, export) + section Stats.</summary>
    public sealed class OutfitsPanel : Panel
    {
        private const int MaxOutfitNameBytes = 31;
        private const int OutfitSlotCount = 20;
        private const int Pad = 8;
        private const int TeleportHeight = 108;
        private const int RightColumnX = 706;
        private const int RightColumnWidth = 274;

        /// <summary>Hauteur totale du panneau; les colonnes la remplissent.</summary>
        public const int DesiredHeight = 666;
        private const string NameCreatorUrl = "https://amethxst.dev/gta-name";

        private static readonly string[] ClothesParts = { "Mask", "Hair", "Gloves", "Legs", "Bag", "Shoes", "Misc", "Undershirt", "Armor", "Decals", "Top" };
        private static readonly string[] PropParts = { "Hat", "Glasses", "Earring", "Watches", "Bracelets" };

        // Libellés FR pour l'affichage; la clé interne reste en anglais (identifiant technique).
        private static readonly Dictionary<string, string> PartLabels = new Dictionary<string, string>
        {
            { "Mask", "Masque" }, { "Hair", "Cheveux" }, { "Gloves", "Gants" }, { "Legs", "Jambes" },
            { "Bag", "Sac" }, { "Shoes", "Chaussures" }, { "Misc", "Divers" }, { "Undershirt", "Sous-vêt." },
            { "Armor", "Armure" }, { "Decals", "Décalcos" }, { "Top", "Haut" },
            { "Hat", "Chapeau" }, { "Glasses", "Lunettes" }, { "Earring", "Boucles" },
            { "Watches", "Montre" }, { "Bracelets", "Bracelets" }
        };

        private const string ImportFormatHelp =
            "Import depuis votre PC : collez le texte ou ouvrez un .txt (voir outfit_example.txt).\r\n\r\n" +
            "Une ligne par composant, draw/texture :";

        private const string ImportFormatExample =
            "name = Ma tenue\r\n" +
            "top = 7/0\r\n" +
            "undershirt = 15/0\r\n" +
            "legs = 10/0\r\n" +
            "feet = 1/0\r\n" +
            "accessories = 0/0\r\n" +
            "bags = 0/0\r\n" +
            "mask = 0/0\r\n" +
            "gloves = 0/0\r\n" +
            "decals = 0/0\r\n" +
            "armor = 0/0\r\n" +
            "hair = 2/0\r\n" +
            "hats = 0/0\r\n" +
            "glasses = 0/0\r\n" +
            "ears = 0/0\r\n" +
            "watches = 0/0\r\n" +
            "bracelets = 0/0";

        private const string ImportFormatNotes =
            "Les lignes manquantes conservent la valeur actuelle de l'emplacement; sans ligne « name = », le nom reste. " +
            "Les tenues sont écrites sur le personnage actuellement chargé en jeu.";

        private readonly IToolHost host;
        private readonly GTAOutfits outfits;
        private readonly Dictionary<string, TextBox> idBoxes = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, TextBox> texBoxes = new Dictionary<string, TextBox>();

        private SlotList slotList;
        private TextBox nameBox;
        private Label nameBytesLabel;
        private Button saveNameButton, saveComponentsButton, applyImportButton, importFileButton, importManyButton;
        private StatsSection statsSection;
        private PasteTextBox importBox;
        private Section slotGroup, formatSection;
        private Section componentsSection, importSection, actionsSection;
        private Label importHintLabel, actionsHintLabel;
        private TileButton teleportBtn, exportClipboardBtn, exportFileBtn, exportManyBtn;
        private bool formattingImport;
        private bool attached;

        public OutfitsPanel(IToolHost host)
        {
            this.host = host;
            outfits = new GTAOutfits(host.Connection);
            Text = "Principal";
            BuildUi();
            Resize += (s, e) => LayoutAdaptive();
            LayoutAdaptive();
        }

        /// <summary>Resize adaptatif du panneau :
        ///   - colonne droite (Format + Stats) adossée au bord droit,
        ///   - colonne milieu (Composants / Import / Téléport et export) occupe toute la
        ///     largeur entre la colonne de gauche et la colonne de droite,
        ///   - Slots s'étire verticalement jusqu'au bas du panneau,
        ///   - les contrôles internes de chaque section du milieu sont relayés pour que
        ///     les boutons et hints remplissent la nouvelle largeur (plus de troncature).
        /// </summary>
        private void LayoutAdaptive()
        {
            if (slotGroup == null || formatSection == null || statsSection == null) return;
            if (componentsSection == null || importSection == null || actionsSection == null) return;

            int clientW = ClientSize.Width;
            int clientH = ClientSize.Height;
            int middleX = 358;
            int rightWidth = RightColumnWidth;

            // Sous ~990 px client, la colonne de droite ne tient plus à côté des deux
            // autres : Format + Stats passent sous la rangée Nom/Slots/Composants.
            bool narrow = clientW < 990;
            int contentHeight = narrow
                ? 666 + formatSection.Height + Pad + 262 + Pad
                : DesiredHeight - Pad;

            // Une barre verticale va apparaître si le contenu dépasse la page : il faut
            // réserver sa largeur AVANT le premier layout, sinon elle déclenche une barre
            // horizontale alors que la colonne droite tenait juste.
            int scrollReserve = !VerticalScroll.Visible && clientH < contentHeight
                ? SystemInformation.VerticalScrollBarWidth
                : 0;
            int usableWidth = Math.Max(0, clientW - scrollReserve);

            // ---- Colonne milieu : occupe toute la place entre gauche et droite
            int rightX = Math.Max(middleX + 340 + Pad, usableWidth - Pad - rightWidth);
            int middleWidth = narrow
                ? Math.Max(340, usableWidth - Pad - middleX)
                : Math.Max(340, rightX - Pad - middleX);
            componentsSection.Width = middleWidth;
            importSection.Width = middleWidth;
            actionsSection.Width = middleWidth;

            // Composants : bouton « Enregistrer » collé à droite
            if (saveComponentsButton != null)
                saveComponentsButton.Left = middleWidth - saveComponentsButton.Width - 8;

            // Import : textbox et hint occupent toute la largeur
            int innerW = middleWidth - 20; // 10 de padding de chaque côté
            importBox.Width = innerW;
            importHintLabel.Width = innerW;

            // Import : 3 boutons étalés en largeur égale (gap = 10)
            LayoutThreeButtons(innerW, 10, 178, 26,
                applyImportButton, importFileButton, importManyButton);

            // Téléport et export : bouton téléport pleine largeur, puis 3 boutons égaux
            teleportBtn.SetBounds(10, 28, innerW, 24);
            actionsHintLabel.Width = innerW;
            LayoutThreeButtons(innerW, 10, 56, 22,
                exportClipboardBtn, exportFileBtn, exportManyBtn);

            // ---- Colonne gauche : les 20 slots doivent rester entièrement visibles
            int slotsY = slotGroup.Top;
            int slotsHeight = Math.Max(22 + slotList.Height + 16, clientH - slotsY - Pad);
            slotGroup.Height = slotsHeight;

            // ---- Colonne droite : à droite en large, empilée sous la rangée en étroit
            if (narrow)
            {
                int below = Math.Max(slotGroup.Bottom, actionsSection.Bottom) + Pad;
                formatSection.SetBounds(10, below, rightWidth, formatSection.Height);
                statsSection.SetBounds(10, below + formatSection.Height + Pad, rightWidth, 262);
            }
            else
            {
                formatSection.SetBounds(rightX, Pad, rightWidth, formatSection.Height);
                int statsTop = formatSection.Bottom + Pad;
                int statsHeight = Math.Max(262, clientH - statsTop - Pad);
                statsSection.SetBounds(rightX, statsTop, rightWidth, statsHeight);
            }

            // Les bornes changent pendant le Resize : sans nouveau passage, WinForms peut
            // conserver une DisplayRectangle calculée sur un état intermédiaire et laisser
            // une barre horizontale fantôme.
            PerformLayout();
        }

        /// <summary>Positionne 3 boutons côte à côte, égaux en largeur, sur une ligne de
        /// <paramref name="innerW"/> pixels de large commençant à x=<paramref name="x"/>.</summary>
        private static void LayoutThreeButtons(int innerW, int x, int y, int h,
            Control b1, Control b2, Control b3)
        {
            int gap = 10;
            int w = (innerW - 2 * gap) / 3;
            if (w < 80) w = 80;
            b1.SetBounds(x, y, w, h);
            b2.SetBounds(x + w + gap, y, w, h);
            b3.SetBounds(x + 2 * (w + gap), y, w, h);
        }

        /// <summary>Appelé quand le jeu est prêt : pointe l'éditeur sur le process et charge les slots.</summary>
        public void Attach(int pid)
        {
            outfits.SetProcess(pid);
            attached = true;
            LoadOutfits(-1);
        }

        private Task TeleportToShop()
        {
            return host.RunAsync("Téléport boutique de vêtements", ClothingShopTeleportOption.RequiredHandlerKeys,
                ClothingShopTeleportOption.OptionalHandlerKeys, ClothingShopTeleportOption.Apply, null, changesStats: false, needsCharacter: false);
        }

        /// <summary>Active les boutons stats uniquement si Personnage 1 ou 2 est choisi dans la barre d'outils.</summary>
        public void SetCharacterChosen(bool chosen)
        {
            statsSection.SetCharacterChosen(chosen);
        }

        public void Detach()
        {
            attached = false;
            outfits.SetProcess(-1);
            slotList.Clear();
            nameBox.Text = string.Empty;
            foreach (TextBox box in idBoxes.Values.Concat(texBoxes.Values))
                box.Text = string.Empty;
        }

        private void BuildUi()
        {
            Section nameGroup = new Section { Text = "Nom", Location = new Point(10, 8), Size = new Size(340, 140) };
            nameBox = new TextBox { Location = new Point(12, 22), Width = 316 };
            nameBox.TextChanged += (s, e) => OnNameEdited();
            nameGroup.Controls.Add(nameBox);
            saveNameButton = new Button { Text = "Enregistrer le nom", Location = new Point(12, 50), Size = new Size(126, 24) };
            saveNameButton.Click += (s, e) => SaveName();
            nameGroup.Controls.Add(saveNameButton);
            nameBytesLabel = new Label { Text = "0/31", AutoSize = true, Location = new Point(146, 54), ForeColor = UiStyle.Muted };
            nameGroup.Controls.Add(nameBytesLabel);
            TileButton createName = new TileButton { Text = "Créer un nom en ligne", Outline = true, Centered = true, Location = new Point(12, 84), Size = new Size(316, 24) };
            createName.Click += (s, e) => LinkOpener.Open(NameCreatorUrl, host.Log);
            nameGroup.Controls.Add(createName);
            nameGroup.Controls.Add(new Label
            {
                Text = "Ouvre le générateur de nom dans votre navigateur.",
                Location = new Point(12, 114), Size = new Size(316, 16), ForeColor = UiStyle.Muted
            });
            Controls.Add(nameGroup);

            slotGroup = new Section { Text = "Emplacements de tenue", Location = new Point(10, 156), Size = new Size(340, DesiredHeight - 156 - Pad) };
            slotList = new SlotList { Location = new Point(8, 22), Size = new Size(324, OutfitSlotCount * SlotList.RowHeight + 2), SlotCount = OutfitSlotCount };
            slotList.SelectedChanged += (s, e) => OnSlotChanged();
            slotGroup.Controls.Add(slotList);
            TileButton refreshButton = new TileButton { Text = "Rafraîchir", Centered = true };
            refreshButton.SetBounds(slotGroup.Width - 90, 2, 82, 16);
            refreshButton.Click += (s, e) => RefreshOutfits();
            slotGroup.Controls.Add(refreshButton);
            Controls.Add(slotGroup);

            componentsSection = new Section { Text = "Composants", Location = new Point(358, 8), Size = new Size(340, 300) };
            componentsSection.Controls.Add(Header("Modèle", 82, 20));
            componentsSection.Controls.Add(Header("Texture", 132, 20));
            componentsSection.Controls.Add(Header("Modèle", 240, 20));
            componentsSection.Controls.Add(Header("Texture", 290, 20));
            AddParts(componentsSection, ClothesParts, 10, 40, 82, 132);
            AddParts(componentsSection, PropParts, 180, 40, 240, 290);

            saveComponentsButton = new Button { Text = "Enregistrer les composants", Location = new Point(132, 160), Size = new Size(200, 28) };
            saveComponentsButton.Click += (s, e) => SaveComponents();
            componentsSection.Controls.Add(saveComponentsButton);
            Controls.Add(componentsSection);

            int importHeight = DesiredHeight - 314 - Pad - TeleportHeight - Pad;
            importSection = new Section { Text = "Import", Location = new Point(358, 314), Size = new Size(340, importHeight) };
            importBox = new PasteTextBox { Location = new Point(10, 22), Size = new Size(320, 148), Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
            importBox.TextChanged += (s, e) => ReformatImportText();
            importSection.Controls.Add(importBox);

            // 3 boutons sur 1 ligne, repositionnés en largeur par LayoutAdaptive().
            applyImportButton = new Button { Text = "Appliquer", Location = new Point(10, 178), Size = new Size(100, 26) };
            applyImportButton.Click += (s, e) => ApplyImportedText(importBox.Text);
            importFileButton = new Button { Text = "1 tenue", Location = new Point(120, 178), Size = new Size(100, 26) };
            importFileButton.Click += (s, e) => ImportFile();
            importManyButton = new Button { Text = "20 tenues", Location = new Point(230, 178), Size = new Size(100, 26) };
            importManyButton.Click += (s, e) => ImportBulkFile();
            importSection.Controls.AddRange(new Control[] { applyImportButton, importFileButton, importManyButton });
            importHintLabel = new Label
            {
                Text = "Appliquer = texte collé ci-dessus · 1 tenue = fichier d'une tenue · 20 tenues = fichier groupé",
                Location = new Point(10, 208), Size = new Size(320, 16), ForeColor = UiStyle.Muted
            };
            importSection.Controls.Add(importHintLabel);
            Controls.Add(importSection);

            // Section combinée : téléport boutique + export de la tenue sélectionnée
            actionsSection = new Section { Text = "Téléport et export", Location = new Point(358, 314 + importHeight + Pad), Size = new Size(340, TeleportHeight) };

            teleportBtn = new TileButton { Text = "Téléport boutique de vêtements", Outline = true, Centered = true };
            teleportBtn.SetBounds(10, 28, 320, 24);
            teleportBtn.Click += async (s, e) => await TeleportToShop();
            actionsSection.Controls.Add(teleportBtn);

            exportClipboardBtn = new TileButton { Text = "Copier", Outline = true, Centered = true };
            exportClipboardBtn.SetBounds(10, 56, 100, 22);
            exportClipboardBtn.Click += (s, e) => ExportSelectedOutfit(toFile: false);
            actionsSection.Controls.Add(exportClipboardBtn);

            exportFileBtn = new TileButton { Text = "1 tenue", Outline = true, Centered = true };
            exportFileBtn.SetBounds(120, 56, 100, 22);
            exportFileBtn.Click += (s, e) => ExportSelectedOutfit(toFile: true);
            actionsSection.Controls.Add(exportFileBtn);

            exportManyBtn = new TileButton { Text = "20 tenues", Outline = true, Centered = true };
            exportManyBtn.SetBounds(230, 56, 100, 22);
            exportManyBtn.Click += (s, e) => ExportBulkFile();
            actionsSection.Controls.Add(exportManyBtn);

            actionsHintLabel = new Label
            {
                Text = "Copier = presse-papier · 1 tenue = fichier d'une tenue · 20 tenues = les 20 tenues",
                Location = new Point(10, 82), Size = new Size(320, 20), ForeColor = UiStyle.Muted
            };
            actionsSection.Controls.Add(actionsHintLabel);

            Controls.Add(actionsSection);

            formatSection = new Section { Text = "Format d'import", Location = new Point(RightColumnX, Pad) };
            int formatY = 26;
            formatY = AddFlowLabel(formatSection, ImportFormatHelp, UiStyle.Muted, null, formatY);
            formatY = AddFlowLabel(formatSection, ImportFormatExample, Theme.Text, Theme.Mono, formatY);
            formatY = AddFlowLabel(formatSection, ImportFormatNotes, UiStyle.Muted, null, formatY);
            formatSection.Size = new Size(RightColumnWidth, formatY + 2);
            Controls.Add(formatSection);

            int statsTopInitial = formatSection.Bottom + Pad;
            statsSection = new StatsSection(host)
            {
                Location = new Point(RightColumnX, statsTopInitial),
                Size = new Size(RightColumnWidth, Math.Max(246, DesiredHeight - statsTopInitial - Pad))
            };
            Controls.Add(statsSection);
        }

        private static int AddFlowLabel(Control parent, string text, Color color, Font font, int y)
        {
            const int width = RightColumnWidth - 20;
            Label label = new Label { Text = text, Location = new Point(10, y), ForeColor = color, UseMnemonic = false, Width = width };
            label.Font = font ?? Theme.Body;
            label.Height = label.GetPreferredSize(new Size(width, 0)).Height + 4;

            parent.Controls.Add(label);
            return y + label.Height + 6;
        }

        private static Label Header(string text, int x, int y)
        {
            return new Label { Text = text, AutoSize = true, Location = new Point(x, y), Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        }

        private void AddParts(Control parent, string[] parts, int x, int y, int idX, int texX)
        {
            foreach (string part in parts)
            {
                string label = PartLabels.TryGetValue(part, out string fr) ? fr : part;
                parent.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(x, y + 4) });
                TextBox id = new TextBox { Location = new Point(idX, y), Width = 46 };
                TextBox tex = new TextBox { Location = new Point(texX, y), Width = 46 };
                parent.Controls.Add(id);
                parent.Controls.Add(tex);
                idBoxes[part] = id;
                texBoxes[part] = tex;
                y += 22;
            }
        }

        // ---- slots ----------------------------------------------------------------------

        private int SelectedSlot()
        {
            return slotList.SelectedSlot;
        }

        /// <summary>Relit les slots depuis le jeu (ex. après passage en ligne ou changement de perso).</summary>
        private void RefreshOutfits()
        {
            if (!attached)
            {
                host.SetStatus("Connectez-vous d'abord au jeu", Color.DarkOrange);
                return;
            }

            LoadOutfits(SelectedSlot());
            host.Log("Liste des tenues rafraîchie.");
            host.SetStatus("Liste des tenues rafraîchie", Color.Blue);
        }

        private void LoadOutfits(int preferredSlot)
        {
            try
            {
                nameBox.Text = string.Empty;
                List<string> names = outfits.ReadNames().Select(name => name?.Trim() ?? string.Empty).ToList();
                slotList.SetNames(names, preferredSlot);
                OnSlotChanged();
            }
            catch (Exception ex)
            {
                host.Log("Erreur liste tenues : " + ex.Message);
            }
        }

        private void OnSlotChanged()
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
            {
                nameBox.Text = string.Empty;
                return;
            }

            nameBox.Text = slotList.NameOf(slot);
            LoadComponents(slot);
        }

        private void LoadComponents(int slot)
        {
            try
            {
                GTAOutfits.OutfitComponents c = outfits.ReadComponents(slot);
                Fill("Mask", c.Mask, c.MaskTex); Fill("Hair", c.Hair, c.HairTex); Fill("Gloves", c.Gloves, c.GlovesTex);
                Fill("Legs", c.Legs, c.LegsTex); Fill("Bag", c.Bag, c.BagTex); Fill("Shoes", c.Shoes, c.ShoesTex);
                Fill("Misc", c.Misc, c.MiscTex); Fill("Undershirt", c.Undershirt, c.UndershirtTex); Fill("Armor", c.Armor, c.ArmorTex);
                Fill("Decals", c.Decals, c.DecalsTex); Fill("Top", c.Top, c.TopTex);
                Fill("Hat", c.Hat, c.HatTex); Fill("Glasses", c.Glasses, c.GlassesTex); Fill("Earring", c.Earring, c.EarringTex);
                Fill("Watches", c.Watches, c.WatchesTex); Fill("Bracelets", c.Bracelets, c.BraceletsTex);
            }
            catch (Exception ex)
            {
                host.Log("Erreur lecture composants : " + ex.Message);
            }
        }

        private void Fill(string part, int draw, int tex)
        {
            idBoxes[part].Text = draw.ToString();
            texBoxes[part].Text = tex.ToString();
        }

        private int Draw(string part) { return int.Parse(idBoxes[part].Text); }

        private int Tex(string part) { return int.Parse(texBoxes[part].Text); }

        private GTAOutfits.OutfitComponents BuildComponentsFromUi()
        {
            return new GTAOutfits.OutfitComponents
            {
                Mask = Draw("Mask"), MaskTex = Tex("Mask"), Hair = Draw("Hair"), HairTex = Tex("Hair"),
                Gloves = Draw("Gloves"), GlovesTex = Tex("Gloves"), Legs = Draw("Legs"), LegsTex = Tex("Legs"),
                Bag = Draw("Bag"), BagTex = Tex("Bag"), Shoes = Draw("Shoes"), ShoesTex = Tex("Shoes"),
                Misc = Draw("Misc"), MiscTex = Tex("Misc"), Undershirt = Draw("Undershirt"), UndershirtTex = Tex("Undershirt"),
                Armor = Draw("Armor"), ArmorTex = Tex("Armor"), Decals = Draw("Decals"), DecalsTex = Tex("Decals"),
                Top = Draw("Top"), TopTex = Tex("Top"), Hat = Draw("Hat"), HatTex = Tex("Hat"),
                Glasses = Draw("Glasses"), GlassesTex = Tex("Glasses"), Earring = Draw("Earring"), EarringTex = Tex("Earring"),
                Watches = Draw("Watches"), WatchesTex = Tex("Watches"), Bracelets = Draw("Bracelets"), BraceletsTex = Tex("Bracelets")
            };
        }

        private void SaveComponents()
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
                return;

            try
            {
                outfits.WriteComponents(slot, BuildComponentsFromUi());
                Notify($"Emplacement {slot + 1} - composants enregistrés !");
                host.SetStatus($"Composants de l'emplacement {slot + 1} enregistrés", Color.Blue);
                host.Log($"Emplacement {slot + 1} : composants enregistrés.");
            }
            catch (FormatException)
            {
                MessageBox.Show("Entrez uniquement des valeurs entières dans les champs.", "Erreur de saisie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur d'enregistrement des composants : " + ex.Message);
            }
        }

        // ---- nom ------------------------------------------------------------------------

        private static int Utf8Bytes(string text)
        {
            return Encoding.UTF8.GetByteCount(text ?? string.Empty);
        }

        /// <summary>31 octets : limite du champ nom côté jeu, non modifiable.</summary>
        private static int CurrentNameLimit()
        {
            return MaxOutfitNameBytes;
        }

        private void OnNameEdited()
        {
            UpdateNameCounter();
        }

        private void UpdateNameCounter()
        {
            int bytes = Utf8Bytes(nameBox.Text);
            int limit = CurrentNameLimit();
            nameBytesLabel.Text = $"{bytes}/{limit}";
            nameBytesLabel.ForeColor = bytes > limit ? Theme.Bad : Theme.Muted;
            saveNameButton.Enabled = attached && SelectedSlot() >= 0 && bytes <= limit;
        }

        private void SaveName()
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
                return;

            try
            {
                outfits.WriteName(slot, nameBox.Text);
                Notify($"Tenue {slot + 1} mise à jour !");
                LoadOutfits(slot);
                host.SetStatus($"Nom de l'emplacement {slot + 1} enregistré", Color.Blue);
                host.Log($"Emplacement {slot + 1} : nom enregistré.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur d'enregistrement : " + ex.Message);
            }
        }

        // ---- export ---------------------------------------------------------------------

        /// <summary>Exporte la tenue sélectionnée au format texte (presse-papier ou fichier).</summary>
        private void ExportSelectedOutfit(bool toFile)
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
            {
                MessageBox.Show("Sélectionnez d'abord un emplacement de tenue.", "Exporter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string outfitName = slotList.NameOf(slot);
                GTAOutfits.OutfitComponents components = outfits.ReadComponents(slot);

                if (toFile)
                {
                    using (SaveFileDialog dialog = new SaveFileDialog
                    {
                        Title = "Exporter la tenue",
                        Filter = "Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
                        FileName = OutfitExport.SuggestFileName(outfitName, slot)
                    })
                    {
                        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                            return;

                        OutfitExport.SaveToFile(dialog.FileName, components, outfitName);
                        host.SetStatus($"Tenue de l'emplacement {slot + 1} exportée", Color.Blue);
                        host.Log($"Export tenue emplacement {slot + 1} → {dialog.FileName}");
                    }
                }
                else
                {
                    OutfitExport.CopyToClipboard(components, outfitName);
                    host.SetStatus($"Tenue de l'emplacement {slot + 1} copiée dans le presse-papier", Color.Blue);
                    host.Log($"Export tenue emplacement {slot + 1} vers presse-papier.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur d'export : " + ex.Message, "Exporter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- import ---------------------------------------------------------------------

        private void ReformatImportText()
        {
            if (formattingImport || importBox.Text.IndexOf('\n') >= 0)
                return;

            if (OutfitImport.TryParse(importBox.Text, out OutfitImport.ImportedOutfit imported, out string ignored) && imported.Parts.Count >= 3)
                SetImportText(OutfitImport.Format(imported));
        }

        private void SetImportText(string text)
        {
            formattingImport = true;
            try
            {
                importBox.Text = text;
                importBox.SelectionStart = importBox.TextLength;
            }
            finally
            {
                formattingImport = false;
            }
        }

        private void ImportFile()
        {
            if (!attached)
                return;

            string text;
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "Importer une tenue", Filter = "Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*" })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                try
                {
                    text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lecture impossible : " + ex.Message, "Erreur d'import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            SetImportText(text);
            ApplyImportedText(text);
        }

        /// <summary>Importe plusieurs tenues depuis un fichier unique avec séparateurs « --- » et remplit les slots dans l'ordre.</summary>
        private void ImportBulkFile()
        {
            if (!attached)
            {
                MessageBox.Show("Connectez-vous d'abord au jeu.", "Import groupé", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string text;
            string fileName;
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Importer un fichier multi-tenues",
                Filter = "Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                try
                {
                    text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    fileName = Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lecture impossible : " + ex.Message, "Import groupé", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (!OutfitBulkImport.TryParse(text, out List<OutfitImport.ImportedOutfit> imported, out string parseError))
            {
                MessageBox.Show(parseError, "Import groupé", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int count = Math.Min(imported.Count, OutfitBulkImport.MaxOutfits);
            if (!host.Confirm($"Importer {count} tenue(s) depuis « {fileName} » ?\n\n" +
                              $"Les {count} premiers emplacement(s) de tenues seront écrasés.",
                              "Import groupé"))
                return;

            int written = 0;
            int nameLimit = CurrentNameLimit();
            List<string> errors = new List<string>();

            for (int i = 0; i < count; i++)
            {
                OutfitImport.ImportedOutfit o = imported[i];

                try
                {
                    if (o.Name != null)
                    {
                        if (Utf8Bytes(o.Name) > nameLimit)
                        {
                            errors.Add($"Emplacement {i + 1} : nom trop long ({Utf8Bytes(o.Name)}/{nameLimit} octets). Nom ignoré, composants écrits.");
                        }
                        else
                        {
                            outfits.WriteName(i, o.Name);
                        }
                    }

                    if (o.Parts.Count > 0)
                        outfits.WriteComponents(i, OutfitImport.ApplyTo(o, outfits.ReadComponents(i)));

                    written++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Emplacement {i + 1} : {ex.Message}");
                }
            }

            Notify($"{written}/{count} tenue(s) importée(s) !");
            LoadOutfits(0);
            host.SetStatus($"{written}/{count} tenue(s) importée(s) depuis {fileName}", Color.Blue);
            host.Log($"Import groupé : {written}/{count} tenue(s) écrite(s) depuis {fileName}.");

            if (errors.Count > 0)
            {
                string summary = string.Join("\n", errors);
                MessageBox.Show($"{errors.Count} problème(s) pendant l'import :\n\n{summary}",
                    "Import groupé", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>Exporte les 20 slots dans un fichier unique avec séparateurs « --- ».</summary>
        private void ExportBulkFile()
        {
            if (!attached)
            {
                MessageBox.Show("Connectez-vous d'abord au jeu.", "Export groupé", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string suggestedName = "tenues_export.txt";
            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Title = $"Exporter les {OutfitSlotCount} tenues",
                Filter = "Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
                FileName = suggestedName
            })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                try
                {
                    List<(string Name, GTAOutfits.OutfitComponents Components)> all = new List<(string, GTAOutfits.OutfitComponents)>();
                    for (int i = 0; i < OutfitSlotCount; i++)
                    {
                        string name = slotList.NameOf(i);
                        GTAOutfits.OutfitComponents c = outfits.ReadComponents(i);
                        all.Add((name, c));
                    }

                    string text = OutfitBulkImport.FormatBulk(all);
                    File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));

                    host.SetStatus($"{OutfitSlotCount} tenues exportées", Color.Blue);
                    host.Log($"Export groupé : {OutfitSlotCount} tenues écrites dans {dialog.FileName}.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erreur d'export : " + ex.Message, "Export groupé", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ApplyImportedText(string text)
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
            {
                MessageBox.Show("Sélectionnez d'abord un emplacement de tenue.", "Importer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Collez d'abord le texte de la tenue.", "Importer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!OutfitImport.TryParse(text, out OutfitImport.ImportedOutfit imported, out string parseError))
            {
                MessageBox.Show(parseError, "Erreur d'import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetImportText(OutfitImport.Format(imported));

            int nameLimit = CurrentNameLimit();
            if (imported.Name != null && Utf8Bytes(imported.Name) > nameLimit)
            {
                MessageBox.Show($"Nom trop long : {Utf8Bytes(imported.Name)}/{nameLimit} octets.\nRien n'a été écrit.",
                    "Limite du nom de tenue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string current = slotList.NameOf(slot);
            if (!host.Confirm($"Écraser l'emplacement {slot + 1} ({current}) avec la tenue importée ?\n\nNom : {imported.Name ?? "(inchangé)"}\nComposants : {imported.Parts.Count}", "Confirmer l'import"))
                return;

            try
            {
                if (imported.Name != null)
                    outfits.WriteName(slot, imported.Name);

                if (imported.Parts.Count > 0)
                    outfits.WriteComponents(slot, OutfitImport.ApplyTo(imported, outfits.ReadComponents(slot)));

                Notify($"Tenue {slot + 1} importée !");
                LoadOutfits(slot);
                host.SetStatus($"Emplacement {slot + 1} importé", Color.Blue);
                host.Log($"Emplacement {slot + 1} : importé.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur d'import : " + ex.Message);
            }
        }

        private void Notify(string message)
        {
            try { host.Connection.Notify(message); } catch { }
        }
    }
}
