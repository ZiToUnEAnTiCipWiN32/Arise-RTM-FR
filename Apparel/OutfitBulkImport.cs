/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

using System.Collections.Generic;
using System.Text;

namespace Arise_RTM
{
    /// <summary>
    /// Import/export d'un fichier unique contenant plusieurs tenues, séparées par une ligne « --- ».
    /// Chaque bloc entre deux séparateurs est au format d'une tenue individuelle (voir
    /// outfit_example.txt). L'ordre des tenues dans le fichier détermine l'ordre des slots à
    /// remplir (première tenue → slot 1, etc.).
    /// </summary>
    public static class OutfitBulkImport
    {
        public const int MaxOutfits = 20;
        public const string SeparatorPrefix = "---";

        /// <summary>
        /// Parse un fichier multi-tenues. Renvoie la liste des tenues valides, dans l'ordre
        /// du fichier. En cas d'erreur de format sur un bloc, renvoie false et décrit l'erreur.
        /// </summary>
        public static bool TryParse(string text, out List<OutfitImport.ImportedOutfit> outfits, out string error)
        {
            outfits = new List<OutfitImport.ImportedOutfit>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Le fichier est vide.";
                return false;
            }

            List<string> blocks = SplitBlocks(text);
            int blockIndex = 0;

            foreach (string rawBlock in blocks)
            {
                string block = rawBlock.Trim();
                if (block.Length == 0) continue;

                blockIndex++;
                if (!OutfitImport.TryParse(block, out OutfitImport.ImportedOutfit outfit, out string blockError))
                {
                    error = $"Erreur sur la tenue #{blockIndex} : {blockError}";
                    return false;
                }

                outfits.Add(outfit);
                if (outfits.Count >= MaxOutfits) break;
            }

            if (outfits.Count == 0)
            {
                error = "Aucune tenue trouvée dans le fichier.";
                return false;
            }

            return true;
        }

        private static List<string> SplitBlocks(string text)
        {
            List<string> blocks = new List<string>();
            StringBuilder current = new StringBuilder();

            foreach (string rawLine in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                string trimmed = rawLine.TrimStart();
                if (trimmed.StartsWith(SeparatorPrefix))
                {
                    if (current.Length > 0)
                    {
                        blocks.Add(current.ToString());
                        current.Length = 0;
                    }
                }
                else
                {
                    current.AppendLine(rawLine);
                }
            }

            if (current.Length > 0)
                blocks.Add(current.ToString());

            return blocks;
        }

        /// <summary>
        /// Formatte plusieurs tenues en un seul texte avec séparateurs « --- » entre deux tenues.
        /// </summary>
        public static string FormatBulk(IList<(string Name, GTAOutfits.OutfitComponents Components)> outfits)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Export multi-tenues - Arise - RTM");
            sb.AppendLine("# Séparateur entre deux tenues : une ligne commençant par ---");
            sb.AppendLine("# Les emplacements seront remplis dans l'ordre d'apparition.");
            sb.AppendLine();

            for (int i = 0; i < outfits.Count; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }

                sb.Append(OutfitExport.Format(outfits[i].Components, outfits[i].Name));
            }

            return sb.ToString();
        }
    }
}
