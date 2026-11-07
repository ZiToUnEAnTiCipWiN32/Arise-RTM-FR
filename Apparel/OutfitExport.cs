/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 Zitoune
 *
 * Ce fichier fait partie du fork et est distribué sous GNU GPL v3 (voir LICENSE).
 */

using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>
    /// Conversion inverse de OutfitImport: prend les composants d'un slot de tenue tels qu'ils
    /// sont lus en mémoire et produit le texte canonique au format outfit_example.txt.
    ///
    /// Le fichier produit peut être rechargé avec OutfitImport.TryParse sans perte.
    /// </summary>
    public static class OutfitExport
    {
        /// <summary>Convertit les composants d'un slot en ImportedOutfit (sans le nom).</summary>
        public static OutfitImport.ImportedOutfit ToImported(GTAOutfits.OutfitComponents c, string name = null)
        {
            OutfitImport.ImportedOutfit outfit = new OutfitImport.ImportedOutfit();
            if (!string.IsNullOrEmpty(name))
                outfit.Name = name;

            outfit.Parts["top"] = new OutfitImport.DrawTex { Draw = c.Top, Tex = c.TopTex };
            outfit.Parts["undershirt"] = new OutfitImport.DrawTex { Draw = c.Undershirt, Tex = c.UndershirtTex };
            outfit.Parts["legs"] = new OutfitImport.DrawTex { Draw = c.Legs, Tex = c.LegsTex };
            outfit.Parts["feet"] = new OutfitImport.DrawTex { Draw = c.Shoes, Tex = c.ShoesTex };
            outfit.Parts["accessories"] = new OutfitImport.DrawTex { Draw = c.Misc, Tex = c.MiscTex };
            outfit.Parts["bags"] = new OutfitImport.DrawTex { Draw = c.Bag, Tex = c.BagTex };
            outfit.Parts["mask"] = new OutfitImport.DrawTex { Draw = c.Mask, Tex = c.MaskTex };
            outfit.Parts["gloves"] = new OutfitImport.DrawTex { Draw = c.Gloves, Tex = c.GlovesTex };
            outfit.Parts["decals"] = new OutfitImport.DrawTex { Draw = c.Decals, Tex = c.DecalsTex };
            outfit.Parts["armor"] = new OutfitImport.DrawTex { Draw = c.Armor, Tex = c.ArmorTex };
            outfit.Parts["hair"] = new OutfitImport.DrawTex { Draw = c.Hair, Tex = c.HairTex };
            outfit.Parts["hats"] = new OutfitImport.DrawTex { Draw = c.Hat, Tex = c.HatTex };
            outfit.Parts["glasses"] = new OutfitImport.DrawTex { Draw = c.Glasses, Tex = c.GlassesTex };
            outfit.Parts["ears"] = new OutfitImport.DrawTex { Draw = c.Earring, Tex = c.EarringTex };
            outfit.Parts["watches"] = new OutfitImport.DrawTex { Draw = c.Watches, Tex = c.WatchesTex };
            outfit.Parts["bracelets"] = new OutfitImport.DrawTex { Draw = c.Bracelets, Tex = c.BraceletsTex };
            return outfit;
        }

        /// <summary>Formatte les composants + nom en texte standard (identique au format d'import).</summary>
        public static string Format(GTAOutfits.OutfitComponents components, string name)
        {
            return OutfitImport.Format(ToImported(components, name));
        }

        /// <summary>Copie le texte exporté dans le presse-papier Windows; retourne le texte produit.</summary>
        public static string CopyToClipboard(GTAOutfits.OutfitComponents components, string name)
        {
            string text = Format(components, name);
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // Clipboard peut échouer si l'IHM n'est pas dispo; non critique.
            }
            return text;
        }

        /// <summary>Enregistre le texte exporté dans un fichier .txt UTF-8.</summary>
        public static void SaveToFile(string path, GTAOutfits.OutfitComponents components, string name)
        {
            string text = Format(components, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        /// <summary>Propose un nom de fichier propre à partir du nom de la tenue.</summary>
        public static string SuggestFileName(string outfitName, int slotIndex)
        {
            string baseName = string.IsNullOrWhiteSpace(outfitName) ? $"tenue_slot_{slotIndex + 1:00}" : outfitName.Trim();
            StringBuilder sb = new StringBuilder(baseName.Length);
            foreach (char c in baseName)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString().Trim().Replace(' ', '_') + ".txt";
        }
    }
}
