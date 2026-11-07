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
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>Une rangée de boutons bascules dont au plus un est sélectionné à la fois
    /// (-1 = aucun). Même style visuel que les boutons de la barre d'outils.</summary>
    internal sealed class ChoiceBar : Panel
    {
        private readonly List<RadioButton> buttons = new List<RadioButton>();
        private bool changing;

        public event EventHandler SelectedChanged;

        public ChoiceBar(IEnumerable<string> labels, int itemWidth, int itemHeight, int selected)
        {
            BackColor = Theme.Panel;
            Tag = "keep";

            int index = 0;
            foreach (string label in labels)
            {
                RadioButton button = new RadioButton
                {
                    Text = label, Location = new Point(index * itemWidth, 0), Size = new Size(itemWidth - 4, itemHeight),
                    Checked = index == selected
                };
                button.CheckedChanged += (s, e) => OnButtonChanged((RadioButton)s);
                buttons.Add(button);
                Controls.Add(button);
                index++;
            }

            Size = new Size(Math.Max(0, index * itemWidth - 4), itemHeight);
            Restyle();
        }

        public int SelectedIndex
        {
            get { return buttons.FindIndex(button => button.Checked); }
            set
            {
                changing = true;
                try
                {
                    for (int i = 0; i < buttons.Count; i++)
                        buttons[i].Checked = i == value;
                }
                finally
                {
                    changing = false;
                }

                Restyle();
            }
        }

        private void OnButtonChanged(RadioButton button)
        {
            Restyle();
            if (!changing && button.Checked)
                SelectedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Restyle()
        {
            foreach (RadioButton button in buttons)
                Theme.StyleToggle(button);
        }
    }
}
