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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Arise_RTM
{
    public enum StatEntryKind { Int, Bool, Float, PackedBool, PackedInt }

    /// <summary>Une écriture lue depuis un fichier de liste.</summary>
    public sealed class StatEntry
    {
        public StatEntryKind Kind;
        public int Line;
        public string Name;       // nom de stat ; « MP_ » représente le personnage choisi
        public int Index;         // index packed
        public int IntValue;
        public bool BoolValue;
        public float FloatValue;
    }

    public sealed class StatListParseResult
    {
        public readonly List<StatEntry> Entries = new List<StatEntry>();
        public readonly List<string> Errors = new List<string>();
    }

    /// <summary>
    /// Liste d'écritures lue depuis un fichier texte simple, une par ligne, « # » démarre un commentaire :
    ///   int    MP_CHAR_ABILITY_1_UNLCK  -1
    ///   bool   MP_SOME_STAT             true
    ///   float  MPPLY_SOME_STAT          6.66
    ///   pbool  1234                     true     (flag packed du personnage choisi)
    ///   pint   1234                     5        (entier packed du personnage choisi)
    /// Un nom commençant par MP_ est écrit sur le personnage choisi (MP0_ ou MP1_) ;
    /// tout autre nom est utilisé tel que tapé.
    /// </summary>
    public static class StatList
    {
        public const int MaxEntries = 20000;
        public const int MaxErrors = 40;
        private const int MaxPackedIndex = 200000;
        private const int QuietAbove = 25;

        private static readonly Regex NamePattern = new Regex("^[A-Za-z0-9_]{1,100}$", RegexOptions.Compiled);

        public static StatListParseResult Parse(string text)
        {
            StatListParseResult result = new StatListParseResult();
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf('#');
                if (comment >= 0)
                    line = line.Substring(0, comment);

                string[] parts = line.Replace('=', ' ').Split(new[] { ' ', '\t', '﻿' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                if (result.Entries.Count >= MaxEntries)
                {
                    AddError(result, i + 1, $"trop d'écritures (limite : {MaxEntries:N0}) ; le reste du fichier a été ignoré.");
                    break;
                }

                if (!TryParseLine(parts, i + 1, out StatEntry entry, out string error))
                {
                    AddError(result, i + 1, error);
                    continue;
                }

                result.Entries.Add(entry);
            }

            return result;
        }

        private static void AddError(StatListParseResult result, int line, string message)
        {
            if (result.Errors.Count < MaxErrors)
                result.Errors.Add($"Ligne {line} : {message}");
        }

        private static bool TryParseLine(string[] parts, int line, out StatEntry entry, out string error)
        {
            entry = new StatEntry { Line = line };
            error = null;

            if (parts.Length != 3)
            {
                error = "3 valeurs attendues : type, nom (ou index) et valeur.";
                return false;
            }

            string type = parts[0].ToLowerInvariant();
            string target = parts[1];
            string value = parts[2];

            switch (type)
            {
                case "int":
                case "bool":
                case "float":
                    if (!NamePattern.IsMatch(target))
                    {
                        error = $"« {Shorten(target)} » n'est pas un nom de stat valide (lettres, chiffres et _ uniquement).";
                        return false;
                    }

                    entry.Name = target;
                    break;

                case "pbool":
                case "packed_bool":
                case "pint":
                case "packed_int":
                    if (!int.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out entry.Index) || entry.Index > MaxPackedIndex)
                    {
                        error = $"« {Shorten(target)} » n'est pas un index packed valide (0 à {MaxPackedIndex}).";
                        return false;
                    }

                    break;

                default:
                    error = $"type « {Shorten(parts[0])} » inconnu (utilisez int, bool, float, pbool ou pint).";
                    return false;
            }

            switch (type)
            {
                case "int":
                case "pint":
                case "packed_int":
                    entry.Kind = type == "int" ? StatEntryKind.Int : StatEntryKind.PackedInt;
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) || number < int.MinValue || number > uint.MaxValue)
                    {
                        error = $"« {Shorten(value)} » n'est pas un entier 32 bits.";
                        return false;
                    }

                    entry.IntValue = unchecked((int)number);
                    return true;

                case "float":
                    entry.Kind = StatEntryKind.Float;
                    if (!float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out entry.FloatValue) ||
                        float.IsNaN(entry.FloatValue) || float.IsInfinity(entry.FloatValue))
                    {
                        error = $"« {Shorten(value)} » n'est pas un nombre.";
                        return false;
                    }

                    return true;

                default:
                    entry.Kind = type == "bool" ? StatEntryKind.Bool : StatEntryKind.PackedBool;
                    if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1")
                        entry.BoolValue = true;
                    else if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
                        entry.BoolValue = false;
                    else
                    {
                        error = $"« {Shorten(value)} » n'est pas true/false (ou 1/0).";
                        return false;
                    }

                    return true;
            }
        }

        private static string Shorten(string text)
        {
            return text.Length <= 40 ? text : text.Substring(0, 37) + "...";
        }

        /// <summary>Nom de stat reçu par le jeu : MP_ devient MP0_ ou MP1_ selon le personnage choisi.</summary>
        public static string ResolveName(string name, int slot)
        {
            return name.StartsWith("MP_", StringComparison.OrdinalIgnoreCase) ? $"MP{slot}_{name.Substring(3)}" : name;
        }

        public static IReadOnlyList<string> RequiredHandlers(IEnumerable<StatEntry> entries)
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (StatEntry entry in entries)
            {
                switch (entry.Kind)
                {
                    case StatEntryKind.Int: keys.Add(HandlerKeys.StatSetInt); break;
                    case StatEntryKind.Bool: keys.Add(HandlerKeys.StatSetBool); break;
                    case StatEntryKind.Float: keys.Add(HandlerKeys.StatSetFloat); break;
                    case StatEntryKind.PackedBool: keys.Add(HandlerKeys.PackedSetBool); break;
                    case StatEntryKind.PackedInt: keys.Add(HandlerKeys.PackedSetInt); break;
                }
            }

            return keys.ToList();
        }

        public static readonly string[] OptionalHandlers =
        {
            HandlerKeys.StatGetInt, HandlerKeys.StatGetBool, HandlerKeys.StatGetFloat, HandlerKeys.PackedGetBool, HandlerKeys.StatSave
        };

        public static int CountFlow(IEnumerable<StatEntry> entries)
        {
            return entries.Count(entry => entry.Name != null && FlowStats.IsFlow(entry.Name));
        }

        /// <summary>Description courte de ce que contient une liste, pour la confirmation et la vérification.</summary>
        public static string Describe(IReadOnlyList<StatEntry> entries)
        {
            int stats = entries.Count(e => e.Kind == StatEntryKind.Int || e.Kind == StatEntryKind.Bool || e.Kind == StatEntryKind.Float);
            int packed = entries.Count - stats;
            string text = $"Écritures : {entries.Count:N0} ({stats:N0} stats, {packed:N0} valeurs packed).";

            int flow = CountFlow(entries);
            if (flow > 0)
                text += $"\n\n[!] {flow:N0} d'entre elles sont des stats de progression histoire/braquages. {FlowStats.Explanation}";

            return text;
        }

        public static OptionResult Run(OptionContext context, IReadOnlyList<StatEntry> entries)
        {
            OptionResult result = new OptionResult();
            context.Verbose = entries.Count <= QuietAbove;

            for (int i = 0; i < entries.Count; i++)
            {
                context.Tick(i, entries.Count);
                StatEntry entry = entries[i];
                string name = entry.Name == null ? null : ResolveName(entry.Name, context.Slot);

                switch (entry.Kind)
                {
                    case StatEntryKind.Int: context.WriteInt(result, name, entry.IntValue); break;
                    case StatEntryKind.Bool: context.WriteBool(result, name, entry.BoolValue); break;
                    case StatEntryKind.Float: context.WriteFloat(result, name, entry.FloatValue); break;
                    case StatEntryKind.PackedBool: context.WritePackedBool(result, entry.Index, entry.BoolValue); break;
                    case StatEntryKind.PackedInt: context.WritePackedInt(result, entry.Index, entry.IntValue); break;
                }
            }

            context.Tick(entries.Count, entries.Count);
            context.SaveIfRequested(result);
            return result;
        }
    }
}
