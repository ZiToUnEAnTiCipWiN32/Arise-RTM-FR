/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine, licence MIT (voir LICENSE-MIT)
 *
 * Ce fichier provient de l'outil d'origine et n'a pas été modifié par le fork.
 */

using System;
using System.Windows.Forms;

namespace Arise_RTM
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool preview = Array.Exists(args, arg => string.Equals(arg, "--preview", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(preview));
        }
    }
}
