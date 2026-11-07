/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine, licence MIT (voir LICENSE-MIT)
 *
 * Ce fichier provient de l'outil d'origine et n'a pas été modifié par le fork.
 */

namespace Arise_RTM
{
    internal static class JenkinsHash
    {
        public static uint Joaat(string text)
        {
            unchecked
            {
                uint hash = 0;
                foreach (char c in text.ToLowerInvariant())
                {
                    hash += (byte)c;
                    hash += hash << 10;
                    hash ^= hash >> 6;
                }

                hash += hash << 3;
                hash ^= hash >> 11;
                hash += hash << 15;
                return hash;
            }
        }
    }
}
