/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System.Collections.Generic;

namespace Arise_RTM
{
    /// <summary>Débloque la course rapide permanente : les six stats d'ability du personnage choisi sont fixées à -1.</summary>
    public sealed class FastRunOption : OptionBase
    {
        private static readonly string[] AbilityStats =
        {
            "CHAR_ABILITY_1_UNLCK", "CHAR_ABILITY_2_UNLCK", "CHAR_ABILITY_3_UNLCK",
            "CHAR_FM_ABILITY_1_UNLCK", "CHAR_FM_ABILITY_2_UNLCK", "CHAR_FM_ABILITY_3_UNLCK"
        };

        public override string Name => "Fast run";

        public override string Description => "Débloque la course rapide permanente pour le personnage choisi.";

        public override IReadOnlyList<string> RequiredHandlers { get; } = new[] { HandlerKeys.StatSetInt };

        public override IReadOnlyList<string> OptionalHandlers { get; } = new[] { HandlerKeys.StatGetInt, HandlerKeys.StatSave };

        public override OptionResult Run(OptionContext context)
        {
            OptionResult result = new OptionResult();

            foreach (string suffix in AbilityStats)
                context.WriteInt(result, context.CharacterStat(suffix), -1);

            context.SaveIfRequested(result);
            return result;
        }
    }
}
