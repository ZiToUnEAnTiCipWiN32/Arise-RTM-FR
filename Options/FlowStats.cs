/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System.Text.RegularExpressions;

namespace Arise_RTM
{
    /// <summary>
    /// Stats de progression histoire/braquages : les écrire peut bloquer les invitations
    /// de Lester et Simeon sur ce personnage. L'onglet Debug avertit avant l'écriture.
    /// </summary>
    public static class FlowStats
    {
        private static readonly Regex Pattern = new Regex(
            "FM_VEH_TX1|_FLOW|H3OPT_|H4CNF_|HEIST_START|HEIST_END|HEIST_PLANNING|H4_PROGRESS|H4_COOLDOWN|" +
            "GANGOPS_HEIST_STATUS|BS_CONTRACT_STRAND|FIXER_STORY_STRAND|CUTSCENE_MID_|HEIST_CUTS_DONE|HEIST_PRE_PLAN",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsFlow(string statName)
        {
            return !string.IsNullOrEmpty(statName) && Pattern.IsMatch(statName);
        }

        public const string Explanation =
            "Ces stats sont des drapeaux de progression histoire/braquages : les écrire peut bloquer les invitations de Lester et Simeon sur ce personnage.";
    }
}
