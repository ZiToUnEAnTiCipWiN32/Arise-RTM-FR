/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

namespace Arise_RTM
{
    /// <summary>
    /// Téléport vers la boutique de vêtements : coordonnées de l'outil d'origine, écrites par
    /// la même routine mémoire que <see cref="TeleportOption"/> (aucune fonction du jeu appelée).
    /// </summary>
    public static class ClothingShopTeleportOption
    {
        public const float ShopX = -814.4328f;
        public const float ShopY = -1085.9870f;
        public const float ShopZ = 10.5673f;

        public static readonly string[] RequiredHandlerKeys = new string[0];
        public static readonly string[] OptionalHandlerKeys = new string[0];

        public static OptionResult Apply(OptionContext context)
        {
            return TeleportOption.ApplyTo(context, ShopX, ShopY, ShopZ, "la boutique de vêtements");
        }
    }
}
