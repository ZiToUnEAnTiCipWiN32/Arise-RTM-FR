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
using System.Threading;

namespace Arise_RTM
{
    public interface IRecoveryOption
    {
        string Name { get; }
        string Description { get; }

        /// <summary>Handlers sans lesquels l'option ne peut pas tourner ; l'exécution est bloquée si l'un manque.</summary>
        IReadOnlyList<string> RequiredHandlers { get; }

        /// <summary>Handlers utilisés quand présents ; ils comptent seulement pour la confirmation du premier lancement.</summary>
        IReadOnlyList<string> OptionalHandlers { get; }

        OptionResult Run(OptionContext context);
    }

    public abstract class OptionBase : IRecoveryOption
    {
        private static readonly string[] None = new string[0];

        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual IReadOnlyList<string> RequiredHandlers => None;
        public virtual IReadOnlyList<string> OptionalHandlers => None;

        public abstract OptionResult Run(OptionContext context);
    }

    public sealed class OptionResult
    {
        public int Written;
        public int Failed;
        public int Mismatched;
        public int Skipped;
        public bool SaveAttempted;
        public bool SaveSucceeded;

        public bool IsClean => Failed == 0 && Mismatched == 0 && Written > 0 && (!SaveAttempted || SaveSucceeded);

        /// <summary>Remplace le résumé par défaut (utilisé par le téléport).</summary>
        public string Note;

        public string Summary => Note ?? $"écrits {Written}, sautés {Skipped}, échecs {Failed}, discordants {Mismatched}";
    }

    /// <summary>Le moteur, le personnage choisi et les bascules Vérifier/Enregistrer. Les helpers Write* appliquent ces bascules.</summary>
    public sealed class OptionContext
    {
        private const float FloatTolerance = 0.0001f;

        public StatEngine Engine { get; set; }
        public int Slot { get; set; }
        public bool Verify { get; set; }
        public bool Save { get; set; }
        public Action<string> Log { get; set; }
        public bool Verbose { get; set; } = true;
        public Action<int, int> Progress { get; set; }
        public CancellationToken Cancel { get; set; }

        public string CharacterStat(string suffix)
        {
            return $"MP{Slot}_{suffix}";
        }

        /// <summary>Signale la progression et stoppe l'exécution si Annuler a été pressé.</summary>
        public void Tick(int done, int total)
        {
            Cancel.ThrowIfCancellationRequested();
            Progress?.Invoke(done, total);
        }

        public void WriteInt(OptionResult result, string statName, int value)
        {
            if (!Engine.SetInt(statName, value))
            {
                WriteAsRealType(result, statName, value);
                return;
            }

            result.Written++;
            if (!Verify)
            {
                Info($"{statName} = {value}");
                return;
            }

            bool read = Engine.TryGetInt(statName, out int actual);
            Report(result, statName, read && actual == value, read ? actual.ToString() : "illisible", value.ToString());
        }

        public void WriteBool(OptionResult result, string statName, bool value)
        {
            if (!Engine.SetBool(statName, value))
            {
                Fail(result, statName, "bool");
                return;
            }

            result.Written++;
            if (!Verify)
            {
                Info($"{statName} = {value}");
                return;
            }

            bool read = Engine.TryGetBool(statName, out bool actual);
            Report(result, statName, read && actual == value, read ? actual.ToString() : "illisible", value.ToString());
        }

        public void WriteFloat(OptionResult result, string statName, float value)
        {
            if (!Engine.SetFloat(statName, value))
            {
                Fail(result, statName, "float");
                return;
            }

            result.Written++;
            if (!Verify)
            {
                Info($"{statName} = {value}");
                return;
            }

            bool read = Engine.TryGetFloat(statName, out float actual);
            Report(result, statName, read && Math.Abs(actual - value) <= FloatTolerance, read ? actual.ToString() : "illisible", value.ToString());
        }

        public void WritePackedBool(OptionResult result, int index, bool value)
        {
            string label = $"packed[{index}] (personnage {Slot})";
            Engine.SetPackedBool(index, value, Slot);
            result.Written++;

            if (!Verify)
            {
                Info($"{label} = {value}");
                return;
            }

            Engine.TryGetPackedBool(index, Slot, out bool actual);
            bool matches = actual == value;
            if (matches)
                Engine.ConfirmHandler(HandlerKeys.PackedSetBool);

            Report(result, label, matches, actual.ToString(), value.ToString());
        }

        public void WritePackedInt(OptionResult result, int index, int value)
        {
            Engine.SetPackedInt(index, value, Slot);
            result.Written++;
            Info($"packed[{index}] (personnage {Slot}) = {value}");
        }

        public void SaveIfRequested(OptionResult result)
        {
            if (!Save || result.Written == 0)
                return;

            result.SaveAttempted = true;
            result.SaveSucceeded = Engine.SaveStats();
            Log?.Invoke(result.SaveSucceeded ? "Sauvegarde des stats demandée." : "L'appel de sauvegarde des stats a renvoyé false.");
        }

        private void Info(string message)
        {
            if (Verbose)
                Log?.Invoke(message);
        }

        /// <summary>Le jeu a refusé une écriture int : lui demander de quel type est réellement la stat et,
        /// si c'est un float ou un bool, l'écrire dans ce type.</summary>
        private void WriteAsRealType(OptionResult result, string statName, int value)
        {
            StatKind kind = Engine.ProbeKind(statName);
            if (kind == StatKind.Float && Engine.SetFloat(statName, value))
            {
                result.Written++;
                Log?.Invoke($"{statName} : stat float, écrite {value}.0 au lieu d'un int.");
                return;
            }

            if (kind == StatKind.Bool && Engine.SetBool(statName, value != 0))
            {
                result.Written++;
                Log?.Invoke($"{statName} : stat bool, écrite {value != 0} au lieu d'un int.");
                return;
            }

            Fail(result, statName, "int", kind);
        }

        private void Fail(OptionResult result, string name, string requested, StatKind? known = null)
        {
            StatKind kind = known ?? Engine.ProbeKind(name);
            if (kind == StatKind.Unknown)
                result.Skipped++;
            else
                result.Failed++;

            string reason = kind == StatKind.Unknown ? "stat inexistante dans ce build du jeu"
                : kind.ToString().Equals(requested, StringComparison.OrdinalIgnoreCase) ? $"c'est bien une stat {requested} mais le jeu a refusé la valeur"
                : $"c'est une stat {kind.ToString().ToLowerInvariant()}, pas {requested}";
            Log?.Invoke($"{name} : refusée en {requested} ({reason}).");
        }

        private void Report(OptionResult result, string name, bool matches, string actual, string expected)
        {
            if (matches)
            {
                Info($"{name} = {expected} (vérifié)");
                return;
            }

            result.Mismatched++;
            Log?.Invoke($"{name} : relecture {actual}, attendu {expected}.");
        }
    }
}
