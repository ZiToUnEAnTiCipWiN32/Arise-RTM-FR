/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>
    /// Onglet « Outils » : niveau/RP, téléports.
    ///
    /// Les sections sont empilées verticalement et s'adaptent dynamiquement à la largeur
    /// du panneau : la méthode <see cref="LayoutAll"/> est rappelée à chaque Resize et
    /// recalcule la largeur de chaque section ainsi que la position/taille de leurs
    /// contrôles internes.
    ///
    /// Les boutons restent désactivés tant que le personnage n'est pas choisi dans la barre d'outils.
    /// </summary>
    public sealed class ToolsPanel : Panel
    {
        private const int Pad = 8;
        private const int MinSectionWidth = 420;
        private const int LevelHeight = 154;
        private const int TeleportMinHeight = 318;

        private readonly IToolHost host;
        private readonly List<Control> characterDependent = new List<Control>();

        // Sections racines
        private Section levelSection, teleportSection;

        // Section Niveau
        private Label levelHelp, levelHint;
        private Label levelRankLabel, levelRpLabel;
        private TextBox rankBox, rpBox;
        private TileButton setRankBtn, setRpBtn;

        // Section Téléports
        private Label teleportHelp, teleportNote;
        private Label teleportLieuLabel, teleportNomLabel, teleportXyzLabel;
        private Label teleportMyPosLabel, teleportMyPosValue;
        private TextBox teleportNameBox, teleportXBox, teleportYBox, teleportZBox;
        private ComboBox teleportCombo;
        private TileButton teleportRefreshBtn, teleportGoBtn, teleportSaveBtn, teleportDeleteBtn, teleportCopyPosBtn;
        private TeleportsFile teleports;
        private System.Windows.Forms.Timer teleportPosTimer;
        private float lastRealX, lastRealY, lastRealZ;
        private bool lastRealValid;
        private bool teleportSyncing;

        public ToolsPanel(IToolHost host)
        {
            this.host = host;
            Text = "Outils";
            BackColor = Theme.Bg;

            BuildLevelSection();
            BuildTeleportSection();

            Resize += (s, e) => LayoutAll();
            LayoutAll();

            SetCharacterChosen(false);

            // Timer de lecture position en temps réel (500 ms). Silencieux tant que le PS4
            // n'est pas connecté — lit juste 24 octets (2 pointeurs) toutes les 500 ms.
            teleportPosTimer = new System.Windows.Forms.Timer { Interval = 500 };
            teleportPosTimer.Tick += (s, e) => UpdateRealTimePosition();
            teleportPosTimer.Start();
            Disposed += (s, e) => teleportPosTimer?.Dispose();
        }

        /// <summary>Active/désactive les boutons dépendants du choix de personnage.</summary>
        public void SetCharacterChosen(bool chosen)
        {
            foreach (Control c in characterDependent)
                c.Enabled = chosen;
        }

        /// <summary>Recalcule la position et la taille des sections, puis replace leurs contrôles.
        /// La section Téléports absorbe la hauteur restante : aucune barre de défilement
        /// n'apparaît tant que la page fait au moins <see cref="CompactHeight"/>.</summary>
        private void LayoutAll()
        {
            if (levelSection == null || teleportSection == null)
                return;

            int compactHeight = 2 * Pad + LevelHeight + Pad + TeleportMinHeight;
            int scrollReserve = !VerticalScroll.Visible && ClientSize.Height < compactHeight
                ? SystemInformation.VerticalScrollBarWidth
                : 0;
            int w = Math.Max(MinSectionWidth, ClientSize.Width - scrollReserve - 2 * Pad);

            int y = Pad;
            levelSection.SetBounds(Pad, y, w, LevelHeight);
            LayoutLevelSection(w);
            y += LevelHeight + Pad;

            int teleportHeight = Math.Max(TeleportMinHeight, ClientSize.Height - y - Pad);
            teleportSection.SetBounds(Pad, y, w, teleportHeight);
            LayoutTeleportSection(w, teleportHeight);

            // Même raison que dans OutfitsPanel : les bornes changent pendant le Resize,
            // un passage final évite une barre horizontale calculée sur un état intermédiaire.
            PerformLayout();
        }

        // ---- Niveau / RP ---------------------------------------------------------------

        private void BuildLevelSection()
        {
            levelSection = new Section { Text = "Niveau (rang / RP)", Location = new Point(Pad, Pad), Size = new Size(MinSectionWidth, LevelHeight) };

            levelHelp = new Label
            {
                Text = "Fixe le rang du personnage choisi (1 à 8000) ou directement le RP cumulé.",
                Location = new Point(10, 22), Size = new Size(MinSectionWidth - 20, 30), ForeColor = Theme.Muted
            };
            levelSection.Controls.Add(levelHelp);

            levelRankLabel = new Label { Text = "Rang", Location = new Point(10, 66), Size = new Size(50, 20), ForeColor = Theme.Muted };
            levelSection.Controls.Add(levelRankLabel);
            rankBox = new TextBox { Text = "120", TextAlign = HorizontalAlignment.Right, Location = new Point(60, 62), Size = new Size(100, 22) };
            levelSection.Controls.Add(rankBox);

            setRankBtn = new TileButton { Text = "Fixer le rang", Primary = true, Centered = true, Location = new Point(170, 62), Size = new Size(200, 22) };
            setRankBtn.Click += async (s, e) =>
            {
                if (!int.TryParse(rankBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rank))
                {
                    MessageBox.Show("Entrez un entier pour le rang (1 à 8000).", "Rang", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                await host.RunAsync($"Rang {rank}", LevelOption.HandlerKeysUsed, new[] { HandlerKeys.StatSave }, ctx => LevelOption.ApplyRank(ctx, rank));
            };
            levelSection.Controls.Add(setRankBtn);
            characterDependent.Add(setRankBtn);

            levelRpLabel = new Label { Text = "RP", Location = new Point(10, 98), Size = new Size(50, 20), ForeColor = Theme.Muted };
            levelSection.Controls.Add(levelRpLabel);
            rpBox = new TextBox { Text = "2165850", TextAlign = HorizontalAlignment.Right, Location = new Point(60, 94), Size = new Size(100, 22) };
            levelSection.Controls.Add(rpBox);

            setRpBtn = new TileButton { Text = "Fixer le RP", Outline = true, Centered = true, Location = new Point(170, 94), Size = new Size(200, 22) };
            setRpBtn.Click += async (s, e) =>
            {
                if (!long.TryParse(rpBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long rp))
                {
                    MessageBox.Show("Entrez un entier pour les RP.", "RP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                await host.RunAsync($"RP {rp:N0}", LevelOption.HandlerKeysUsed, new[] { HandlerKeys.StatSave }, ctx => LevelOption.ApplyRp(ctx, rp));
            };
            levelSection.Controls.Add(setRpBtn);
            characterDependent.Add(setRpBtn);

            levelHint = new Label
            {
                Text = "Rangs usuels : 10 → 24 000 RP · 100 → 1 584 350 RP · 120 → 2 165 850 RP · " +
                       "500 → 17 M · 1000 → 47,5 M · 8000 → ~1,79 Md.",
                Location = new Point(10, 120), Size = new Size(MinSectionWidth - 20, 30), ForeColor = Theme.Muted
            };
            levelSection.Controls.Add(levelHint);

            Controls.Add(levelSection);
        }

        private void LayoutLevelSection(int w)
        {
            levelHelp.Width = w - 20;
            levelHint.Width = w - 20;
            // Les 2 boutons principaux remplissent l'espace à droite de la textbox
            int btnLeft = 170;
            int btnRight = w - 10;
            int btnWidth = Math.Max(120, btnRight - btnLeft);
            setRankBtn.SetBounds(btnLeft, 62, btnWidth, 22);
            setRpBtn.SetBounds(btnLeft, 94, btnWidth, 22);
        }

        // ---- Téléports -----------------------------------------------------------------

        private void BuildTeleportSection()
        {
            int y = 174;
            teleportSection = new Section { Text = "Téléports", Location = new Point(Pad, y), Size = new Size(MinSectionWidth, TeleportMinHeight) };

            // Chargement du fichier (créé automatiquement au premier lancement avec 16 lieux par défaut)
            teleports = new TeleportsFile(AppDomain.CurrentDomain.BaseDirectory);
            try
            {
                teleports.LoadOrSeed();
                host.Log($"{teleports.Entries.Count} téléport(s) chargé(s) depuis {teleports.FilePath}.");
            }
            catch (Exception ex)
            {
                host.Log("Erreur chargement téléports : " + ex.Message);
            }

            teleportHelp = new Label
            {
                Text = "Sélectionnez un lieu pour voir ses coordonnées, modifiez-les si besoin, puis téléportez-vous. " +
                       "Vous pouvez aussi renommer, ajouter ou supprimer des lieux — ils sont enregistrés dans teleports.txt.",
                Location = new Point(10, 22), Size = new Size(MinSectionWidth - 20, 30), ForeColor = Theme.Muted
            };
            teleportSection.Controls.Add(teleportHelp);

            teleportLieuLabel = new Label { Text = "Lieu", Location = new Point(10, 60), Size = new Size(40, 20), ForeColor = Theme.Muted };
            teleportSection.Controls.Add(teleportLieuLabel);
            teleportCombo = new ComboBox
            {
                Location = new Point(50, 56), Size = new Size(200, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Input, ForeColor = Theme.Text
            };
            teleportCombo.SelectedIndexChanged += (s, e) => OnTeleportSelectionChanged();
            teleportSection.Controls.Add(teleportCombo);

            teleportRefreshBtn = new TileButton { Text = "Rafraîchir", Outline = true, Centered = true, Size = new Size(110, 22) };
            teleportRefreshBtn.Click += (s, e) => ReloadTeleports();
            teleportSection.Controls.Add(teleportRefreshBtn);

            teleportNomLabel = new Label { Text = "Nom", Location = new Point(10, 90), Size = new Size(40, 20), ForeColor = Theme.Muted };
            teleportSection.Controls.Add(teleportNomLabel);
            teleportNameBox = new TextBox { Location = new Point(50, 86), Size = new Size(200, 22) };
            teleportSection.Controls.Add(teleportNameBox);

            teleportXyzLabel = new Label { Text = "X, Y, Z", Location = new Point(10, 120), Size = new Size(60, 20), ForeColor = Theme.Muted };
            teleportSection.Controls.Add(teleportXyzLabel);
            teleportXBox = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Right, Location = new Point(75, 116), Size = new Size(100, 22) };
            teleportYBox = new TextBox { Text = "0", TextAlign = HorizontalAlignment.Right, Location = new Point(185, 116), Size = new Size(100, 22) };
            teleportZBox = new TextBox { Text = "72", TextAlign = HorizontalAlignment.Right, Location = new Point(295, 116), Size = new Size(100, 22) };
            teleportSection.Controls.Add(teleportXBox);
            teleportSection.Controls.Add(teleportYBox);
            teleportSection.Controls.Add(teleportZBox);

            // Rangée « Ma position » : lecture live des coordonnées du joueur + bouton pour
            // les copier dans les champs X/Y/Z (permet d'enregistrer le lieu courant).
            teleportMyPosLabel = new Label { Text = "Ma position", Location = new Point(10, 150), Size = new Size(70, 20), ForeColor = Theme.Muted };
            teleportSection.Controls.Add(teleportMyPosLabel);
            teleportMyPosValue = new Label
            {
                Text = "—",
                Location = new Point(85, 150), Size = new Size(200, 20),
                ForeColor = Theme.Yellow, Font = Theme.Mono, TextAlign = ContentAlignment.MiddleLeft
            };
            teleportSection.Controls.Add(teleportMyPosValue);
            teleportCopyPosBtn = new TileButton { Text = "Copier dans X, Y, Z", Outline = true, Centered = true, Size = new Size(140, 22), Enabled = false };
            teleportCopyPosBtn.Click += (s, e) => CopyRealTimePositionToFields();
            teleportSection.Controls.Add(teleportCopyPosBtn);

            teleportGoBtn = new TileButton { Text = "Téléporter", Primary = true, Centered = true, Location = new Point(10, 178), Size = new Size(130, 26) };
            teleportGoBtn.Click += async (s, e) => await OnTeleportClicked();
            teleportSection.Controls.Add(teleportGoBtn);

            teleportSaveBtn = new TileButton { Text = "Enregistrer", Outline = true, Centered = true, Location = new Point(150, 178), Size = new Size(130, 26) };
            teleportSaveBtn.Click += (s, e) => OnSaveTeleport();
            teleportSection.Controls.Add(teleportSaveBtn);

            teleportDeleteBtn = new TileButton { Text = "Supprimer", Outline = true, Centered = true, Location = new Point(290, 178), Size = new Size(130, 26) };
            teleportDeleteBtn.Click += (s, e) => OnDeleteTeleport();
            teleportSection.Controls.Add(teleportDeleteBtn);

            teleportNote = new Label
            {
                Text = "« Ma position » affiche les coordonnées du personnage en jeu (rafraîchies toutes les 500 ms). " +
                       "Cliquez « Copier dans X, Y, Z » pour les reprendre, saisissez un nom puis « Enregistrer » pour sauver le lieu. " +
                       "Si le lieu de téléport vous fait tomber sous la carte, relevez le Z de quelques mètres.",
                Location = new Point(10, 210), Size = new Size(MinSectionWidth - 20, 76), ForeColor = Theme.Muted
            };
            teleportSection.Controls.Add(teleportNote);

            Controls.Add(teleportSection);

            RefreshTeleportCombo();
        }

        private void LayoutTeleportSection(int w, int h)
        {
            teleportHelp.Width = w - 20;
            teleportNote.Width = w - 20;
            teleportNote.Height = Math.Max(56, h - teleportNote.Top - 8);

            // Rangée 1 : combobox s'étire, Rafraîchir reste à droite
            int comboLeft = 50;
            int refreshW = 110;
            int gap = 10;
            int refreshLeft = w - 10 - refreshW;
            int comboWidth = Math.Max(140, refreshLeft - gap - comboLeft);
            teleportCombo.SetBounds(comboLeft, 56, comboWidth, 22);
            teleportRefreshBtn.SetBounds(refreshLeft, 56, refreshW, 22);

            // Rangée 2 : Nom pleine largeur à droite de « Nom »
            int nameLeft = 50;
            int nameRight = w - 10;
            teleportNameBox.SetBounds(nameLeft, 86, Math.Max(140, nameRight - nameLeft), 22);

            // Rangée 3 : trois textbox égales (X/Y/Z), avec label XYZ à gauche (sans chevauchement)
            int xyzLeft = 75;
            int xyzRight = w - 10;
            int xyzWidth = xyzRight - xyzLeft;
            int boxW = (xyzWidth - 2 * gap) / 3;
            if (boxW < 60) boxW = 60;
            teleportXBox.SetBounds(xyzLeft, 116, boxW, 22);
            teleportYBox.SetBounds(xyzLeft + boxW + gap, 116, boxW, 22);
            teleportZBox.SetBounds(xyzLeft + 2 * (boxW + gap), 116, boxW, 22);

            // Rangée 4 : « Ma position » (label + valeur qui s'étire + bouton Copier à droite)
            int copyW = 140;
            int copyLeft = w - 10 - copyW;
            int valueLeft = 85;
            int valueWidth = Math.Max(120, copyLeft - gap - valueLeft);
            teleportMyPosValue.SetBounds(valueLeft, 150, valueWidth, 20);
            teleportCopyPosBtn.SetBounds(copyLeft, 147, copyW, 22);

            // Rangée 5 : trois boutons égaux (Téléporter / Enregistrer / Supprimer)
            int btnLeft = 10;
            int btnRight = w - 10;
            int btnTotal = btnRight - btnLeft;
            int btnW = (btnTotal - 2 * gap) / 3;
            if (btnW < 90) btnW = 90;
            teleportGoBtn.SetBounds(btnLeft, 178, btnW, 26);
            teleportSaveBtn.SetBounds(btnLeft + btnW + gap, 178, btnW, 26);
            teleportDeleteBtn.SetBounds(btnLeft + 2 * (btnW + gap), 178, btnW, 26);
        }

        private void RefreshTeleportCombo()
        {
            if (teleportCombo == null || teleports == null) return;

            teleportSyncing = true;
            try
            {
                teleportCombo.Items.Clear();
                foreach (TeleportLocation loc in teleports.Entries)
                    teleportCombo.Items.Add(loc);

                if (teleportCombo.Items.Count > 0)
                    teleportCombo.SelectedIndex = 0;
                else
                    ClearTeleportFields();
            }
            finally
            {
                teleportSyncing = false;
            }
            OnTeleportSelectionChanged();
        }

        private void OnTeleportSelectionChanged()
        {
            if (teleportSyncing) return;
            if (teleportCombo.SelectedItem is TeleportLocation loc)
            {
                teleportNameBox.Text = loc.Name;
                teleportXBox.Text = loc.X.ToString("0.0000", CultureInfo.InvariantCulture);
                teleportYBox.Text = loc.Y.ToString("0.0000", CultureInfo.InvariantCulture);
                teleportZBox.Text = loc.Z.ToString("0.0000", CultureInfo.InvariantCulture);
            }
        }

        private void ClearTeleportFields()
        {
            teleportNameBox.Text = string.Empty;
            teleportXBox.Text = "0";
            teleportYBox.Text = "0";
            teleportZBox.Text = "72";
        }

        private void ReloadTeleports()
        {
            try
            {
                teleports.LoadOrSeed();
                RefreshTeleportCombo();
                host.Log($"Téléports rechargés ({teleports.Entries.Count}).");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de recharger teleports.txt : " + ex.Message, "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task OnTeleportClicked()
        {
            if (!TryFloat(teleportXBox.Text, out float x) ||
                !TryFloat(teleportYBox.Text, out float y) ||
                !TryFloat(teleportZBox.Text, out float z))
            {
                MessageBox.Show("Entrez trois nombres pour X, Y et Z.", "Téléport", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string label = teleportNameBox.Text.Trim();
            if (label.Length == 0)
                label = $"({x:0}, {y:0}, {z:0})";

            await host.RunAsync($"Téléport : {label}",
                TeleportOption.RequiredHandlerKeys, TeleportOption.OptionalHandlerKeys,
                ctx => TeleportOption.ApplyTo(ctx, x, y, z, label),
                changesStats: false,
                needsCharacter: false);
        }

        private void OnSaveTeleport()
        {
            string newName = teleportNameBox.Text.Trim();
            if (newName.Length == 0)
            {
                MessageBox.Show("Donnez un nom au lieu avant de l'enregistrer.", "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TryFloat(teleportXBox.Text, out float x) ||
                !TryFloat(teleportYBox.Text, out float y) ||
                !TryFloat(teleportZBox.Text, out float z))
            {
                MessageBox.Show("X, Y et Z doivent être des nombres valides.", "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TeleportLocation selected = teleportCombo.SelectedItem as TeleportLocation;
            bool isRename = selected != null && !string.Equals(selected.Name, newName, StringComparison.OrdinalIgnoreCase);

            if (isRename)
            {
                if (teleports.IndexOf(newName) >= 0)
                {
                    MessageBox.Show($"Le nom « {newName} » est déjà utilisé par un autre lieu.", "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!host.Confirm($"Renommer « {selected.Name} » en « {newName} » et mettre à jour ses coordonnées ?", "Renommer un lieu"))
                    return;

                if (!teleports.Rename(selected.Name, newName, x, y, z))
                {
                    MessageBox.Show("Impossible de renommer ce lieu.", "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                host.Log($"Lieu renommé : « {selected.Name} » → « {newName} ».");
            }
            else if (teleports.IndexOf(newName) >= 0)
            {
                if (!host.Confirm($"« {newName} » existe déjà. Écraser ses coordonnées actuelles ?", "Écraser un lieu"))
                    return;

                teleports.Update(newName, x, y, z);
                host.Log($"Lieu mis à jour : « {newName} ».");
            }
            else
            {
                if (!teleports.Add(newName, x, y, z))
                {
                    MessageBox.Show("Impossible d'ajouter ce lieu.", "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                host.Log($"Lieu ajouté : « {newName} ».");
            }

            RefreshTeleportCombo();
            SelectTeleportByName(newName);
        }

        private void OnDeleteTeleport()
        {
            TeleportLocation selected = teleportCombo.SelectedItem as TeleportLocation;
            if (selected == null)
            {
                MessageBox.Show("Sélectionnez d'abord un lieu à supprimer.", "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!host.Confirm($"Supprimer définitivement « {selected.Name} » du fichier ?", "Supprimer un lieu"))
                return;

            string removedName = selected.Name;
            if (!teleports.Remove(removedName))
            {
                MessageBox.Show("Impossible de supprimer ce lieu.", "Téléports", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            host.Log($"Lieu supprimé : « {removedName} ».");
            RefreshTeleportCombo();
        }

        private void SelectTeleportByName(string name)
        {
            for (int i = 0; i < teleportCombo.Items.Count; i++)
            {
                if (teleportCombo.Items[i] is TeleportLocation loc &&
                    string.Equals(loc.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    teleportCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // ---- Position temps réel --------------------------------------------------------

        /// <summary>Tente de lire la position du joueur (24 octets au total). Silencieux
        /// tant que la PS4 n'est pas connectée — affiche « — » dans ce cas.</summary>
        private void UpdateRealTimePosition()
        {
            if (host != null && host.IsBusy) return;
            if (teleportMyPosValue == null || teleportCopyPosBtn == null) return;

            StatEngine engine = host?.Engine;
            if (engine == null)
            {
                SetRealPosUnavailable();
                return;
            }

            try
            {
                if (PlayerPedMemory.TryLocate(engine, out PlayerPedMemory.Location loc) &&
                    PlayerPedMemory.TryReadPosition(engine, loc.TransformPosition, out float x, out float y, out float z))
                {
                    lastRealX = x;
                    lastRealY = y;
                    lastRealZ = z;
                    lastRealValid = true;
                    teleportMyPosValue.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.00}, {1:0.00}, {2:0.00}", x, y, z);
                    teleportCopyPosBtn.Enabled = true;
                }
                else
                {
                    SetRealPosUnavailable();
                }
            }
            catch
            {
                SetRealPosUnavailable();
            }
        }

        private void SetRealPosUnavailable()
        {
            lastRealValid = false;
            teleportMyPosValue.Text = "—";
            teleportCopyPosBtn.Enabled = false;
        }

        private void CopyRealTimePositionToFields()
        {
            if (!lastRealValid) return;
            teleportXBox.Text = lastRealX.ToString("0.0000", CultureInfo.InvariantCulture);
            teleportYBox.Text = lastRealY.ToString("0.0000", CultureInfo.InvariantCulture);
            teleportZBox.Text = lastRealZ.ToString("0.0000", CultureInfo.InvariantCulture);
            // Déselectionner le lieu courant pour éviter d'écraser ses coordonnées par
            // inadvertance : l'utilisateur va saisir un nouveau nom.
            teleportSyncing = true;
            try { teleportCombo.SelectedIndex = -1; }
            finally { teleportSyncing = false; }
            teleportNameBox.Focus();
            teleportNameBox.SelectAll();
            host.Log($"Position copiée dans X, Y, Z : {teleportMyPosValue.Text}");
        }
    }
}
