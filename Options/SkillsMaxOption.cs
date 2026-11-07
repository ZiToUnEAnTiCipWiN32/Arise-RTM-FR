/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

using System.Collections.Generic;

namespace Arise_RTM
{
    /// <summary>
    /// Met les 7 compétences standard du personnage choisi à 100 (max) sur GTA V 1.57 PS4 EU,
    /// puis pousse PLAYER_MENTAL_STATE à 100 (float).
    ///
    /// Le menu Stats de GTA Online est piloté par les stats SCRIPT_INCREASE_* :
    ///   SCRIPT_INCREASE_STAM  — endurance (course longue)
    ///   SCRIPT_INCREASE_SHO   — tir (précision, recul, zoom, chargeur)
    ///   SCRIPT_INCREASE_STRN  — force (coups de poing, grimper)
    ///   SCRIPT_INCREASE_STL   — discrétion (silence des pas, radar ennemi)
    ///   SCRIPT_INCREASE_FLY   — pilotage (stabilité des avions/hélicos)
    ///   SCRIPT_INCREASE_DRIV  — conduite (wheelies, contrôle en moto)
    ///   SCRIPT_INCREASE_LUNG  — apnée (temps sous l'eau)
    ///
    /// Les stats STAMINA / SHOOTING_ABILITY / ... existent aussi, mais le jeu ignore
    /// les écritures dessus : elles ne mettent pas les barres du menu à jour.
    /// PLAYER_MENTAL_STATE reste une stat float et se met à jour directement.
    /// </summary>
    public sealed class SkillsMaxOption : OptionBase
    {
        private const int MaxSkill = 100;

        private static readonly string[] SkillStats =
        {
            "SCRIPT_INCREASE_STAM",
            "SCRIPT_INCREASE_SHO",
            "SCRIPT_INCREASE_STRN",
            "SCRIPT_INCREASE_STL",
            "SCRIPT_INCREASE_FLY",
            "SCRIPT_INCREASE_DRIV",
            "SCRIPT_INCREASE_LUNG"
        };

        public override string Name => "Compétences au max";

        public override string Description =>
            "Met les 7 compétences du personnage choisi à 100 (endurance, tir, force, " +
            "discrétion, pilotage, conduite, apnée) et pousse l'état psychiatrique au maximum.";

        public override IReadOnlyList<string> RequiredHandlers { get; } = new[] { HandlerKeys.StatSetInt };

        public override IReadOnlyList<string> OptionalHandlers { get; } = new[]
        {
            HandlerKeys.StatGetInt, HandlerKeys.StatSetFloat, HandlerKeys.StatGetFloat, HandlerKeys.StatSave
        };

        public override OptionResult Run(OptionContext context)
        {
            OptionResult result = new OptionResult();
            foreach (string suffix in SkillStats)
                context.WriteInt(result, context.CharacterStat(suffix), MaxSkill);

            string mentalState = context.CharacterStat("PLAYER_MENTAL_STATE");
            if (context.Engine.IsAvailable(HandlerKeys.StatSetFloat))
                context.WriteFloat(result, mentalState, MaxSkill);
            else
                context.WriteInt(result, mentalState, MaxSkill);

            context.SaveIfRequested(result);
            return result;
        }
    }
}
