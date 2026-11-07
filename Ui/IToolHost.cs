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
using System.Threading.Tasks;

namespace Arise_RTM
{
    /// <summary>Ce dont les onglets ont besoin de la fenêtre principale : le moteur, le
    /// personnage choisi, et un moyen contrôlé de lancer une opération.</summary>
    public interface IToolHost
    {
        StatEngine Engine { get; }
        PS4Connection Connection { get; }

        /// <summary>Slot du personnage choisi (0 = Personnage 1, 1 = Personnage 2), -1 si aucun.</summary>
        int Slot { get; }

        /// <summary>Vrai pendant une opération : les lectures périodiques doivent s'abstenir
        /// pour ne pas partager le socket ps4debug avec l'opération en cours.</summary>
        bool IsBusy { get; }

        /// <summary>Choisit Personnage 1 (0) ou Personnage 2 (1), comme le fait la barre d'outils.</summary>
        void ChooseCharacter(int slot);

        void Log(string message);
        void SetStatus(string text, Color color);
        bool Confirm(string message, string caption);

        /// <summary>
        /// Lance <paramref name="work"/> avec l'UI bloquée, une barre de progression et un
        /// bouton Annuler. Prend en charge les vérifications communes à toute opération sur
        /// les stats : personnage choisi, handlers disponibles, confirmation, avertissement
        /// sur un handler non vérifié, jeu toujours actif.
        /// <paramref name="confirmDetails"/> force l'affichage de la confirmation même si
        /// « Demander avant d'appliquer » est désactivé. <paramref name="changesStats"/>
        /// vaut false pour les lectures et les téléports. <paramref name="needsCharacter"/>
        /// vaut false pour ce qui ne dépend pas du personnage.
        /// </summary>
        Task RunAsync(string title, IEnumerable<string> requiredHandlers, IEnumerable<string> optionalHandlers,
            Func<OptionContext, OptionResult> work, string confirmDetails = null, bool changesStats = true, bool needsCharacter = true);

        Task RunOptionAsync(IRecoveryOption option);
    }

    internal static class UiStyle
    {
        public static readonly Color Muted = Theme.Muted;
    }
}
