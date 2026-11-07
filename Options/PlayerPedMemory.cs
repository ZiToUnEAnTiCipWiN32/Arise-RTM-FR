/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;

namespace Arise_RTM
{
    /// <summary>
    /// Emplacement du ped local et de sa position dans le jeu (GTA V PS4 1.57), déduit du code du
    /// handler player_ped_id et confirmé par une sonde en direct : les deux copies de la position
    /// contenaient les mêmes coordonnées. Seule la mémoire est lue ou écrite ici, aucun code du
    /// jeu n'est exécuté.
    /// </summary>
    public static class PlayerPedMemory
    {
        // 0xEAB010 (the instruction after the lea in player_ped_id) + 0x27CD6F8: a pointer to the ped factory; factory + 8 = local ped
        public const ulong FactoryPointerAddress = 0x3678708UL;
        public const int FactoryPedOffset = 8;
        public const int PedTransformOffset = 0x30;
        public const int TransformPositionOffset = 0x50;
        public const int PedPositionOffset = 0x90;

        private const ulong MinPointer = 0x10000UL;
        private const ulong MaxPointer = 0x800000000000UL;

        public sealed class Location
        {
            public ulong Factory;
            public ulong Ped;
            public ulong Transform;

            public ulong TransformPosition => Transform + (ulong)TransformPositionOffset;
            public ulong PedPosition => Ped + (ulong)PedPositionOffset;
        }

        public static bool TryLocate(StatEngine engine, out Location location)
        {
            location = null;
            if (!TryReadPointer(engine, FactoryPointerAddress, out ulong factory) ||
                !TryReadPointer(engine, factory + (ulong)FactoryPedOffset, out ulong ped) ||
                !TryReadPointer(engine, ped + (ulong)PedTransformOffset, out ulong transform))
                return false;

            location = new Location { Factory = factory, Ped = ped, Transform = transform };
            return true;
        }

        public static bool TryReadPosition(StatEngine engine, ulong address, out float x, out float y, out float z)
        {
            x = y = z = 0f;
            byte[] raw = engine.ReadGameMemory(address, 12);
            if (raw == null || raw.Length != 12)
                return false;

            x = BitConverter.ToSingle(raw, 0);
            y = BitConverter.ToSingle(raw, 4);
            z = BitConverter.ToSingle(raw, 8);
            return IsFinite(x) && IsFinite(y) && IsFinite(z);
        }

        public static void WritePosition(StatEngine engine, ulong address, float x, float y, float z)
        {
            byte[] raw = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, raw, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, raw, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(z), 0, raw, 8, 4);
            engine.WriteGameMemory(address, raw);
        }

        public static bool TryReadPointer(StatEngine engine, ulong address, out ulong value)
        {
            value = 0;
            byte[] raw = engine.ReadGameMemory(address, 8);
            if (raw == null || raw.Length != 8)
                return false;

            value = BitConverter.ToUInt64(raw, 0);
            return value > MinPointer && value < MaxPointer;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
