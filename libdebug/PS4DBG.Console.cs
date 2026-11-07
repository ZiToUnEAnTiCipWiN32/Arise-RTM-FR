using System.Text;

namespace libdebug {

    public partial class PS4DBG {

        //console
        // packet sizes
        // send size
        private const int CMD_CONSOLE_PRINT_PACKET_SIZE = 4;

        private const int CMD_CONSOLE_NOTIFY_PACKET_SIZE = 8;

        // console
        // note: the disconnect command actually uses the console api to end the connection
        /// <summary>
        /// Reboot console
        /// </summary>
        public void Reboot() {
            CheckConnected();

            SendCMDPacket(CMDS.CMD_CONSOLE_REBOOT, 0);
            IsConnected = false;
        }

        /// <summary>
        /// Print to serial port (UTF-8).
        /// </summary>
        public void Print(string str) {
            CheckConnected();

            string raw = str + "\0";
            byte[] bytes = Encoding.UTF8.GetBytes(raw);

            SendCMDPacket(CMDS.CMD_CONSOLE_PRINT, CMD_CONSOLE_PRINT_PACKET_SIZE, bytes.Length);
            SendData(bytes, bytes.Length);
            CheckStatus();
        }

        /// <summary>
        /// Notify console (UTF-8 — nécessaire pour afficher les accents côté toast PS4).
        /// </summary>
        public void Notify(int messageType, string message) {
            CheckConnected();

            string raw = message + "\0";
            byte[] bytes = Encoding.UTF8.GetBytes(raw);

            SendCMDPacket(CMDS.CMD_CONSOLE_NOTIFY, CMD_CONSOLE_NOTIFY_PACKET_SIZE, messageType, bytes.Length);
            SendData(bytes, bytes.Length);
            CheckStatus();
        }

        /// <summary>
        /// Console information
        /// </summary>
        public void GetConsoleInformation() {
            CheckConnected();

            SendCMDPacket(CMDS.CMD_CONSOLE_INFO, 0);
            CheckStatus();

            // TODO return the data
        }
    }
}