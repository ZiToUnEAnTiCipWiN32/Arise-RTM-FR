/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;
using System.Diagnostics;

namespace Arise_RTM
{
    /// <summary>Ouvre une adresse web ou un fichier local avec l'application par défaut.</summary>
    internal static class LinkOpener
    {
        public static void Open(string target, Action<string> log)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log?.Invoke("Ouverture impossible de " + target + " : " + ex.Message);
            }
        }
    }
}
