/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using libdebug;

namespace Arise_RTM
{
    public class PS4Connection
    {
        private readonly object ioLock = new object();
        private PS4DBG ps4;
        public bool IsConnected { get; private set; } = false;

        public bool IsDebugActive(string ip)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var result = client.BeginConnect(ip, 744, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public bool InjectPayload(string ip, string payloadPath)
        {
            return InjectPayload(ip, payloadPath, 9090);
        }

        public bool InjectPayload(string ip, string payloadPath, int payloadPort)
        {
            try
            {
                if (!File.Exists(payloadPath)) return false;
                byte[] data = File.ReadAllBytes(payloadPath);

                using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    sock.ReceiveTimeout = 3000;
                    sock.SendTimeout = 3000;
                    sock.Connect(ip, payloadPort);
                    sock.Send(data);
                    sock.Close();
                }
                return true;
            }
            catch { return false; }
        }

        public void Connect(string ip)
        {
            lock (ioLock)
            {
                ps4 = new PS4DBG(ip);
                IsConnected = ps4.Connect();
            }
        }

        public void Disconnect()
        {
            lock (ioLock)
            {
                if (ps4 != null)
                {
                    try { ps4.Disconnect(); } catch { }
                    ps4 = null;
                }

                IsConnected = false;
            }
        }

        public void Notify(string message)
        {
            lock (ioLock)
            {
                if (IsConnected) ps4.Notify(222, message);
            }
        }

        public int FindProcessID(string name)
        {
            lock (ioLock)
            {
                if (!IsConnected || ps4 == null) return -1;

                var list = ps4.GetProcessList();
                var proc = list.FindProcess(name);

                return (proc != null) ? proc.pid : -1;
            }
        }

        public int GetConsoleFirmwareRaw()
        {
            lock (ioLock)
            {
                if (!IsConnected || ps4 == null) return 0;
                return ps4.GetExtFWVersion();
            }
        }

        public string GetConsoleFirmwareText()
        {
            int raw = GetConsoleFirmwareRaw();
            if (raw <= 0) return "N/D";

            int major = raw / 100;
            int minor = raw % 100;
            return $"{major}.{minor:00}";
        }

        public bool TryGetProcessInfo(int pid, out ProcessInfo processInfo)
        {
            processInfo = new ProcessInfo();
            if (pid <= 0) return false;

            lock (ioLock)
            {
                if (!IsConnected || ps4 == null) return false;

                try
                {
                    processInfo = ps4.GetProcessInfo(pid);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool TryGetGamePatchVersionFromFtp(
            string consoleIp,
            string titleId,
            out string appVersion,
            out string packageVersion,
            out string sourcePath,
            out string errorInfo,
            int preferredPort)
        {
            appVersion = string.Empty;
            packageVersion = string.Empty;
            sourcePath = string.Empty;
            errorInfo = string.Empty;

            if (string.IsNullOrWhiteSpace(consoleIp) || string.IsNullOrWhiteSpace(titleId))
            {
                errorInfo = "IP de console ou Title ID invalide.";
                return false;
            }

            string cleanTitleId = titleId.Trim().ToUpperInvariant();
            string[] candidatePaths =
            {
                $"/system_data/priv/appmeta/{cleanTitleId}/param.sfo",
                $"/user/appmeta/{cleanTitleId}/param.sfo",
                $"/mnt/sandbox/{cleanTitleId}_000/app0/sce_sys/param.sfo"
            };
            StringBuilder attempts = new StringBuilder();
            int[] ftpPorts = BuildFtpPorts(preferredPort);

            foreach (string remotePath in candidatePaths)
            {
                if (!TryDownloadFileFromFtp(consoleIp, remotePath, ftpPorts, out byte[] sfoBytes, out string ftpError))
                {
                    if (attempts.Length > 0) attempts.Append(" | ");
                    attempts.Append($"{remotePath} -> {ftpError}");
                    continue;
                }

                if (sfoBytes != null && sfoBytes.Length > 0 &&
                    TryParseSfoVersions(sfoBytes, out appVersion, out packageVersion))
                {
                    sourcePath = remotePath;
                    return true;
                }

                if (attempts.Length > 0) attempts.Append(" | ");
                attempts.Append($"{remotePath} -> téléchargé, mais le param.sfo n'a pas pu être analysé.");
            }

            errorInfo = attempts.Length > 0 ? attempts.ToString() : "Aucune tentative FTP n'a été effectuée.";
            return false;
        }

        private static int[] BuildFtpPorts(int preferredPort)
        {
            if (preferredPort > 0 && preferredPort <= 65535)
            {
                if (preferredPort == 2121) return new[] { 2121, 21 };
                if (preferredPort == 21) return new[] { 21, 2121 };
                return new[] { preferredPort, 2121, 21 };
            }

            return new[] { 2121, 21 };
        }

        private static bool TryParseSfoVersions(byte[] data, out string appVersion, out string packageVersion)
        {
            appVersion = string.Empty;
            packageVersion = string.Empty;

            int sfoOffset = FindSfoHeaderOffset(data);
            if (sfoOffset < 0)
                return false;

            if (!TryReadUInt32(data, sfoOffset + 0x08, out uint keyTableOffset)) return false;
            if (!TryReadUInt32(data, sfoOffset + 0x0C, out uint dataTableOffset)) return false;
            if (!TryReadUInt32(data, sfoOffset + 0x10, out uint entryCount)) return false;

            if (entryCount == 0 || entryCount > 1024)
                return false;

            int indexTableStart = sfoOffset + 0x14;
            int keyTableStart = sfoOffset + (int)keyTableOffset;
            int valueTableStart = sfoOffset + (int)dataTableOffset;

            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = indexTableStart + (i * 0x10);
                if (entryOffset + 0x10 > data.Length)
                    break;

                ushort keyOffset = BitConverter.ToUInt16(data, entryOffset + 0x00);
                ushort format = BitConverter.ToUInt16(data, entryOffset + 0x02);
                uint paramLen = BitConverter.ToUInt32(data, entryOffset + 0x04);
                uint valueOffset = BitConverter.ToUInt32(data, entryOffset + 0x0C);

                int keyStart = keyTableStart + keyOffset;
                string key = ReadNullTerminatedAscii(data, keyStart);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                int valueStart = valueTableStart + (int)valueOffset;
                string value = ReadSfoStringValue(data, valueStart, paramLen, format);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (key.Equals("APP_VER", StringComparison.OrdinalIgnoreCase))
                    appVersion = value;
                else if (key.Equals("VERSION", StringComparison.OrdinalIgnoreCase))
                    packageVersion = value;
            }

            return !string.IsNullOrWhiteSpace(appVersion) || !string.IsNullOrWhiteSpace(packageVersion);
        }

        private static int FindSfoHeaderOffset(byte[] data)
        {
            for (int i = 0; i <= data.Length - 4; i++)
            {
                if (data[i] == 0x00 && data[i + 1] == 0x50 && data[i + 2] == 0x53 && data[i + 3] == 0x46)
                    return i;
            }
            return -1;
        }

        private static bool TryReadUInt32(byte[] data, int offset, out uint value)
        {
            value = 0;
            if (offset < 0 || offset + 4 > data.Length)
                return false;
            value = BitConverter.ToUInt32(data, offset);
            return true;
        }

        private static string ReadNullTerminatedAscii(byte[] data, int start)
        {
            if (start < 0 || start >= data.Length)
                return string.Empty;

            int end = start;
            while (end < data.Length && data[end] != 0x00)
                end++;

            int length = end - start;
            if (length <= 0)
                return string.Empty;

            return Encoding.ASCII.GetString(data, start, length).Trim();
        }

        private static string ReadSfoStringValue(byte[] data, int start, uint length, ushort format)
        {
            if (start < 0 || start >= data.Length || length == 0)
                return string.Empty;

            int maxLength = (int)Math.Min(length, (uint)(data.Length - start));
            if (maxLength <= 0)
                return string.Empty;

            if (format == 0x0204 || format == 0x0004)
            {
                string raw = Encoding.UTF8.GetString(data, start, maxLength);
                int nullIndex = raw.IndexOf('\0');
                if (nullIndex >= 0) raw = raw.Substring(0, nullIndex);
                return raw.Trim();
            }

            return string.Empty;
        }

        private static bool TryDownloadFileFromFtp(
            string consoleIp,
            string remotePath,
            int[] ftpPorts,
            out byte[] data,
            out string errorInfo)
        {
            data = null;
            errorInfo = string.Empty;

            NetworkCredential[] credentials =
            {
                new NetworkCredential("anonymous", "anonymous"),
                new NetworkCredential("anonymous", string.Empty),
                new NetworkCredential(string.Empty, string.Empty)
            };

            bool[] passiveModes = { true, false };
            foreach (NetworkCredential credential in credentials)
            {
                foreach (bool usePassive in passiveModes)
                {
                    foreach (int ftpPort in ftpPorts)
                    {
                        try
                        {
                            string url = $"ftp://{consoleIp}:{ftpPort}{remotePath}";
                            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url);
                            request.Method = WebRequestMethods.Ftp.DownloadFile;
                            request.Credentials = credential;
                            request.UseBinary = true;
                            request.UsePassive = usePassive;
                            request.KeepAlive = false;
                            request.EnableSsl = false;
                            request.Proxy = null;
                            request.Timeout = 8000;
                            request.ReadWriteTimeout = 8000;

                            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                            using (Stream responseStream = response.GetResponseStream())
                            using (MemoryStream memory = new MemoryStream())
                            {
                                if (responseStream == null)
                                {
                                    errorInfo = $"port {ftpPort}, passif={usePassive} : flux de réponse vide";
                                    continue;
                                }

                                responseStream.CopyTo(memory);
                                data = memory.ToArray();
                                if (data.Length > 0)
                                    return true;

                                errorInfo = $"port {ftpPort}, passif={usePassive} : fichier vide";
                            }
                        }
                        catch (Exception ex)
                        {
                            errorInfo = $"port {ftpPort}, passif={usePassive} : {ex.Message}";
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(errorInfo))
                errorInfo = "Échec du téléchargement FTP pour une raison inconnue";

            return false;
        }

        public byte[] ReadMemory(int pid, ulong address, int size)
        {
            lock (ioLock)
            {
                if (!IsConnected || ps4 == null) return null;
                return ps4.ReadMemory(pid, address, size);
            }
        }

        public void WriteMemory(int pid, ulong address, byte[] data)
        {
            lock (ioLock)
            {
                if (IsConnected && ps4 != null) ps4.WriteMemory(pid, address, data);
            }
        }

        private void RequireConnection()
        {
            if (!IsConnected || ps4 == null)
                throw new InvalidOperationException("Non connecté à la PS4.");
        }

        public ProcessMap GetProcessMaps(int pid)
        {
            lock (ioLock)
            {
                RequireConnection();
                return ps4.GetProcessMaps(pid);
            }
        }

        public ulong InstallRpc(int pid)
        {
            lock (ioLock)
            {
                RequireConnection();
                return ps4.InstallRPC(pid);
            }
        }

        public ulong CallFunction(int pid, ulong rpcStub, ulong address, params object[] args)
        {
            lock (ioLock)
            {
                RequireConnection();
                return ps4.Call(pid, rpcStub, address, args);
            }
        }

        public ulong AllocateMemory(int pid, int length)
        {
            lock (ioLock)
            {
                RequireConnection();
                return ps4.AllocateMemory(pid, length);
            }
        }
    }
}
