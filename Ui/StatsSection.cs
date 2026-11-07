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
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>
    /// Déblocage fast run et K/D personnalisé (ratio, éliminations, morts) pour le personnage
    /// choisi dans la barre d'outils. Les boutons restent désactivés tant qu'il n'est pas choisi.
    /// </summary>
    internal sealed class StatsSection : Section
    {
        private const int TileHeight = 24;
        private const int BoxWidth = 64;

        private static readonly string[] KdRequired = { HandlerKeys.StatSetInt, HandlerKeys.StatSetFloat };
        private static readonly string[] KdOptional = { HandlerKeys.StatGetInt, HandlerKeys.StatSave };

        private readonly IToolHost host;
        private readonly List<TileButton> actionButtons = new List<TileButton>();

        public StatsSection(IToolHost host)
        {
            this.host = host;
            Text = "Statistiques";
            BuildUi();
            SetCharacterChosen(false);
        }

        /// <summary>N'active les boutons que si Personnage 1 ou 2 est choisi dans la barre d'outils.</summary>
        public void SetCharacterChosen(bool chosen)
        {
            foreach (TileButton button in actionButtons)
                button.Enabled = chosen;
        }

        private void BuildUi()
        {
            TileButton unlock = new TileButton { Text = "Débloquer le fast run", Primary = true, Centered = true };
            unlock.SetBounds(10, 30, 254, TileHeight);
            unlock.Click += async (s, e) => await host.RunOptionAsync(new FastRunOption());
            actionButtons.Add(unlock);
            Controls.Add(unlock);

            TileButton skillsMax = new TileButton { Text = "Compétences au max (8 stats à 100)", Primary = true, Centered = true };
            skillsMax.SetBounds(10, 60, 254, TileHeight);
            skillsMax.Click += async (s, e) => await host.RunOptionAsync(new SkillsMaxOption());
            actionButtons.Add(skillsMax);
            Controls.Add(skillsMax);

            TextBox ratio = new TextBox { Text = "6.66", TextAlign = HorizontalAlignment.Right };
            AddRow(ratio, "K/D", "Fixer K/D", 100, async () =>
            {
                if (!TryParseDouble(ratio.Text, out double value))
                {
                    MessageBox.Show("Entrez un nombre pour le ratio K/D (ex : 6.66).", "K/D", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await RunKd($"K/D {value.ToString(CultureInfo.InvariantCulture)}", ctx => KdOption.ApplyRatio(ctx, value));
            });

            TextBox kills = new TextBox { TextAlign = HorizontalAlignment.Right };
            AddRow(kills, "Élim.", "Fixer élim.", 132, () =>
                ApplyNumber(kills, "Élim.", value => RunKd($"Éliminations {value}", ctx => KdOption.ApplyKills(ctx, value)), "éliminations"));

            TextBox deaths = new TextBox { TextAlign = HorizontalAlignment.Right };
            AddRow(deaths, "Morts", "Fixer morts", 164, () =>
                ApplyNumber(deaths, "Morts", value => RunKd($"Morts {value}", ctx => KdOption.ApplyDeaths(ctx, value))));

            Controls.Add(new Label
            {
                Text = "Choisissez Personnage 1 ou 2 dans la barre d'outils pour activer les boutons.",
                Location = new Point(10, 200), Size = new Size(254, 54), ForeColor = UiStyle.Muted
            });
        }

        private void AddRow(TextBox box, string label, string buttonText, int y, Func<Task> click)
        {
            const int buttonLeft = 56 + BoxWidth + 6;
            Controls.Add(new Label { Text = label, Location = new Point(10, y + 5), Size = new Size(44, 16), ForeColor = Theme.Muted });
            box.SetBounds(56, y, BoxWidth, TileHeight);
            box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(box);

            TileButton button = new TileButton { Text = buttonText, Outline = true, Centered = true };
            button.SetBounds(buttonLeft, y, 264 - buttonLeft, TileHeight);
            button.Click += async (s, e) => await click();
            actionButtons.Add(button);
            Controls.Add(button);
        }

        private async Task ApplyNumber(TextBox box, string label, Func<long, Task> apply, string errorName = null)
        {
            if (!long.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                MessageBox.Show($"Entrez un nombre entier pour {errorName ?? label.ToLowerInvariant()}.", label, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await apply(value);
        }

        private Task RunKd(string title, Func<OptionContext, OptionResult> work)
        {
            return host.RunAsync(title, KdRequired, KdOptional, work);
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
