/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>TextBox qui préserve les sauts de ligne au collage, même si le texte source
    /// utilise uniquement des LF ou des CR seuls.</summary>
    public class PasteTextBox : TextBox
    {
        private const int WM_PASTE = 0x0302;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PASTE)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string text = Clipboard.GetText()
                            .Replace("\r\n", "\n")
                            .Replace('\r', '\n')
                            .Replace("\n", Environment.NewLine);
                        SelectedText = text;
                        return;
                    }
                }
                catch (ExternalException)
                {
                }
            }

            base.WndProc(ref m);
        }
    }
}
