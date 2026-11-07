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
using System.Linq;
using libdebug;

namespace Arise_RTM
{
    public enum StatKind { Unknown, Int, Bool, Float }

    /// <summary>
    /// Appelle les handlers natifs du jeu via le stub RPC de ps4debug.
    /// Un handler est de la forme void f(NativeArg_s*) : un pointeur vers un contexte dont
    /// ArgValues (ctx+0x10) pointe vers des slots d'arguments de 8 octets. La valeur de retour
    /// est écrite dans ReturnValue[0]. Le précontrôle est en lecture seule ; le stub RPC n'est
    /// installé qu'au premier appel réel.
    /// </summary>
    public sealed class StatEngine
    {
        private const int SignatureLength = 16;
        private const int ScratchSize = 0x1000;
        private const int ProtExecute = 0x4;

        private const int ArgsOffset = 0x100;
        private const int OutputOffset = 0x200;
        private const int MaxNativeArgs = 8;

        /// <summary>
        /// Prologue attendu pour un handler natif thread-safe (stats, packed) : push rbp; mov rbp, rsp.
        /// Les wrappers tail-call (48 8B 47 10 mov rax, [rdi+0x10]) étaient aussi acceptés mais ils
        /// correspondent à des natives qui touchent au script host (ped, vehicle, streaming) ; les
        /// appeler depuis un thread RPC externe provoque un kernel panic — ces options ont été
        /// retirées, le preflight n'accepte donc plus que le prologue classique.
        /// </summary>
        private static readonly byte[] EntryPrologue = { 0x55, 0x48, 0x89, 0xE5 };

        private sealed class HandlerState
        {
            public string Key;
            public ulong Relative;
            public ulong Absolute;
            public bool IsCore;
            public bool Available;
            public bool Verified;
            public bool Succeeded;
            public byte[] Pending;
        }

        private readonly PS4Connection connection;
        private readonly GameProfile profile;
        private readonly SignatureStore signatures;
        private readonly Action<string> log;
        private readonly Dictionary<string, HandlerState> handlers = new Dictionary<string, HandlerState>(StringComparer.Ordinal);

        private int pid = -1;
        private string titleId;
        private ulong rpcStub;
        private ulong scratchBuffer;

        public bool IsReady { get; private set; }

        public StatEngine(PS4Connection connection, GameProfile profile, SignatureStore signatures, Action<string> log)
        {
            this.connection = connection;
            this.profile = profile;
            this.signatures = signatures;
            this.log = log;
        }

        public void Reset()
        {
            IsReady = false;
            pid = -1;
            titleId = null;
            rpcStub = 0;
            scratchBuffer = 0;
            handlers.Clear();
        }

        public bool IsAvailable(string key)
        {
            return handlers.TryGetValue(key, out HandlerState handler) && handler.Available;
        }

        public bool IsVerified(string key)
        {
            return handlers.TryGetValue(key, out HandlerState handler) && handler.Available && handler.Verified;
        }

        public void ConfirmHandler(string key)
        {
            if (handlers.TryGetValue(key, out HandlerState handler))
                handler.Succeeded = true;
        }

        public bool Preflight(int gamePid, string gameTitleId, out string failure)
        {
            Reset();
            failure = null;
            pid = gamePid;
            titleId = gameTitleId;

            ProcessMap map = connection.GetProcessMaps(pid);
            if (map == null || map.entries == null || map.entries.Length == 0)
            {
                failure = "Impossible de lire la carte mémoire du processus.";
                return false;
            }

            MemoryEntry image = FindMainImage(map);
            if (image != null)
                log($"Image principale « {image.name} » à 0x{image.start:X} (address_base du profil 0x{profile.AddressBase:X}).");

            foreach (KeyValuePair<string, ulong> pair in profile.Handlers)
            {
                HandlerState handler = new HandlerState
                {
                    Key = pair.Key,
                    Relative = pair.Value,
                    Absolute = profile.AddressBase + pair.Value,
                    IsCore = HandlerKeys.Core.Contains(pair.Key)
                };
                handlers[pair.Key] = handler;

                if (CheckHandler(map, handler, out string problem))
                    continue;

                if (handler.IsCore)
                {
                    failure = problem;
                    return false;
                }

                handler.Available = false;
                log("AVERTISSEMENT (handler facultatif désactivé) : " + problem);
            }

            IsReady = true;
            return true;
        }

        // ---- stat writes ----------------------------------------------------------------

        public bool SetInt(string statName, int value)
        {
            ulong result = CallNative(HandlerKeys.StatSetInt, Hash(statName), unchecked((uint)value), 1);
            return Succeeded(HandlerKeys.StatSetInt, result);
        }

        public bool SetBool(string statName, bool value)
        {
            ulong result = CallNative(HandlerKeys.StatSetBool, Hash(statName), value ? 1UL : 0UL, 1);
            return Succeeded(HandlerKeys.StatSetBool, result);
        }

        public bool SetFloat(string statName, float value)
        {
            ulong result = CallNative(HandlerKeys.StatSetFloat, Hash(statName), FloatBits(value), 1);
            return Succeeded(HandlerKeys.StatSetFloat, result);
        }

        public void SetPackedBool(int index, bool value, int slot)
        {
            CallNative(HandlerKeys.PackedSetBool, unchecked((uint)index), value ? 1UL : 0UL, unchecked((uint)slot));
        }

        public void SetPackedInt(int index, int value, int slot)
        {
            CallNative(HandlerKeys.PackedSetInt, unchecked((uint)index), unchecked((uint)value), unchecked((uint)slot));
        }

        /// <summary>Which type the game reports for a stat name, by trying the typed readers (read-only). Unknown: no reader accepted the name.</summary>
        public StatKind ProbeKind(string statName)
        {
            try
            {
                if (IsAvailable(HandlerKeys.StatGetFloat) && TryGetFloat(statName, out _))
                    return StatKind.Float;
                if (IsAvailable(HandlerKeys.StatGetBool) && TryGetBool(statName, out _))
                    return StatKind.Bool;
                if (IsAvailable(HandlerKeys.StatGetInt) && TryGetInt(statName, out _))
                    return StatKind.Int;
            }
            catch (InvalidOperationException)
            {
            }

            return StatKind.Unknown;
        }

        public bool SaveStats()
        {
            ulong result = CallNative(HandlerKeys.StatSave, 0, 0, 3, 0);
            return Succeeded(HandlerKeys.StatSave, result);
        }

        // ---- stat reads -----------------------------------------------------------------

        public bool TryGetInt(string statName, out int value)
        {
            bool ok = TryGetRaw(HandlerKeys.StatGetInt, statName, out uint raw);
            value = unchecked((int)raw);
            return ok;
        }

        public bool TryGetBool(string statName, out bool value)
        {
            bool ok = TryGetRaw(HandlerKeys.StatGetBool, statName, out uint raw);
            value = raw != 0;
            return ok;
        }

        public bool TryGetFloat(string statName, out float value)
        {
            bool ok = TryGetRaw(HandlerKeys.StatGetFloat, statName, out uint raw);
            value = BitConverter.ToSingle(BitConverter.GetBytes(raw), 0);
            return ok;
        }

        public bool TryGetPackedBool(int index, int slot, out bool value)
        {
            ulong result = CallNative(HandlerKeys.PackedGetBool, unchecked((uint)index), unchecked((uint)slot));
            handlers[HandlerKeys.PackedGetBool].Succeeded = true;
            value = (result & 0xFF) != 0;
            return true;
        }

        /// <summary>Read-only: copies game memory at an absolute address. Executes nothing.</summary>
        public byte[] ReadGameMemory(ulong address, int length)
        {
            return IsReady ? connection.ReadMemory(pid, address, length) : null;
        }

        /// <summary>Writes game memory at an absolute address (no game code runs). Only for structures whose layout was confirmed.</summary>
        public void WriteGameMemory(ulong address, byte[] data)
        {
            if (!IsReady)
                throw new InvalidOperationException("Le précontrôle n'a pas réussi.");

            connection.WriteMemory(pid, address, data);
        }

        public int CommitSignatures()
        {
            int stored = 0;
            foreach (HandlerState handler in handlers.Values)
            {
                if (!handler.Available || !handler.Succeeded || handler.Pending == null)
                    continue;

                signatures.Save(titleId, handler.Key, handler.Pending);
                handler.Pending = null;
                handler.Verified = true;
                stored++;
            }

            if (stored > 0)
                log($"Signatures enregistrées pour {titleId} : {stored} handler(s).");

            return stored;
        }

        // ---- internals ------------------------------------------------------------------

        private static uint Hash(string statName)
        {
            return JenkinsHash.Joaat(statName);
        }

        private static ulong FloatBits(float value)
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
        }

        private bool Succeeded(string key, ulong result)
        {
            bool ok = (result & 0xFF) != 0;
            if (ok)
                handlers[key].Succeeded = true;

            return ok;
        }

        private bool TryGetRaw(string key, string statName, out uint raw)
        {
            raw = 0;
            EnsureRpc();

            ulong outputAddress = scratchBuffer + OutputOffset;
            connection.WriteMemory(pid, outputAddress, new byte[8]);

            ulong result = CallNative(key, Hash(statName), outputAddress, 0xFFFFFFFFUL);
            if (!Succeeded(key, result))
                return false;

            byte[] data = connection.ReadMemory(pid, outputAddress, 4);
            if (data == null || data.Length < 4)
                return false;

            raw = BitConverter.ToUInt32(data, 0);
            return true;
        }

        private static MemoryEntry FindMainImage(ProcessMap map)
        {
            return map.entries
                .Where(e => e != null && e.name != null &&
                            (e.name.IndexOf("executable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             e.name.IndexOf("eboot", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(e => e.start)
                .FirstOrDefault();
        }

        private bool CheckHandler(ProcessMap map, HandlerState handler, out string problem)
        {
            problem = null;

            MemoryEntry region = FindRegion(map, handler.Absolute);
            if (region == null)
            {
                problem = $"{handler.Key} : adresse 0x{handler.Absolute:X} non mappée. Vérifiez address_base dans profile.ini.";
                return false;
            }

            if ((region.prot & ProtExecute) == 0)
            {
                problem = $"{handler.Key} : l'adresse 0x{handler.Absolute:X} n'est pas dans une région exécutable ({region.name}).";
                return false;
            }

            byte[] code = connection.ReadMemory(pid, handler.Absolute, SignatureLength);
            if (code == null || code.Length < SignatureLength)
            {
                problem = $"{handler.Key} : lecture impossible de {SignatureLength} octets à 0x{handler.Absolute:X}.";
                return false;
            }

            if (!code.Take(EntryPrologue.Length).SequenceEqual(EntryPrologue))
            {
                problem = $"{handler.Key} : 0x{handler.Absolute:X} n'est pas une entrée de fonction (attendu {ToHex(EntryPrologue)}, trouvé {ToHex(code)}).\n" +
                          "Rien n'a été appelé. Vérifiez address_base dans profile.ini.";
                return false;
            }

            string state;
            if (signatures.TryGet(titleId, handler.Key, out byte[] expected))
            {
                if (!expected.SequenceEqual(code))
                {
                    problem = $"{handler.Key} : les octets à 0x{handler.Absolute:X} diffèrent de la signature enregistrée.\n" +
                              $"Trouvé  : {ToHex(code)}\nAttendu : {ToHex(expected)}";
                    return false;
                }

                handler.Verified = true;
                state = "entrée OK, signature concordante";
            }
            else
            {
                handler.Pending = code;
                state = "entrée OK, NON VÉRIFIÉ (aucune signature enregistrée)";
            }

            handler.Available = true;
            log($"{handler.Key} @ 0x{handler.Absolute:X} [{region.name}] {ToHex(code)} - {state}");
            return true;
        }

        private static MemoryEntry FindRegion(ProcessMap map, ulong address)
        {
            return map.entries.FirstOrDefault(e => e != null && address >= e.start && address < e.end);
        }

        private void EnsureRpc()
        {
            if (!IsReady)
                throw new InvalidOperationException("Le précontrôle n'a pas réussi.");

            if (rpcStub != 0)
                return;

            rpcStub = connection.InstallRpc(pid);
            if (rpcStub == 0)
                throw new InvalidOperationException("L'installation du RPC a renvoyé un stub nul.");

            scratchBuffer = connection.AllocateMemory(pid, ScratchSize);
            if (scratchBuffer == 0)
                throw new InvalidOperationException("Impossible d'allouer le tampon de travail dans le processus du jeu.");

            log($"Stub RPC à 0x{rpcStub:X}, tampon de travail à 0x{scratchBuffer:X}.");
        }

        private ulong CallNative(string key, params ulong[] args)
        {
            ExecuteHandler(key, args);

            byte[] returned = connection.ReadMemory(pid, scratchBuffer + ArgsOffset, 8);
            return returned != null && returned.Length == 8 ? BitConverter.ToUInt64(returned, 0) : 0;
        }

        private void ExecuteHandler(string key, ulong[] args)
        {
            EnsureRpc();
            if (args.Length > MaxNativeArgs)
                throw new ArgumentException("Trop d'arguments natifs.");

            if (!handlers.TryGetValue(key, out HandlerState handler) || !handler.Available)
                throw new InvalidOperationException($"Handler « {key} » indisponible sur ce build.");

            ulong argsAddress = scratchBuffer + ArgsOffset;
            byte[] block = new byte[ArgsOffset + MaxNativeArgs * 8];

            // NativeArg_s: ReturnValue @0x00, ArgCount @0x08, ArgValues @0x10, ReadCount @0x18
            Buffer.BlockCopy(BitConverter.GetBytes(argsAddress), 0, block, 0x00, 8);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)args.Length), 0, block, 0x08, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(argsAddress), 0, block, 0x10, 8);

            for (int i = 0; i < args.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(args[i]), 0, block, ArgsOffset + i * 8, 8);

            connection.WriteMemory(pid, scratchBuffer, block);
            connection.CallFunction(pid, rpcStub, handler.Absolute, scratchBuffer);
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", " ");
        }
    }
}
