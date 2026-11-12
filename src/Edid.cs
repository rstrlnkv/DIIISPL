// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Text;
using Microsoft.Win32;

namespace DIIISPL
{
    /// <summary>
    /// Parses a monitor's EDID. Gives a stable identity that does not depend on
    /// which port the cable is plugged into or what Windows thinks about "monitor numbers".
    /// </summary>
    internal class EdidInfo
    {
        public string Manufacturer;      // three-letter PNP code, e.g. GSM
        public ushort ProductCode;
        public uint SerialNumber;        // numeric serial from the header
        public string SerialText;        // serial as a string (descriptor 0xFF)
        public string ModelName;         // model name (descriptor 0xFC)
        public int PreferredWidth;
        public int PreferredHeight;
        public int WidthMm;
        public int HeightMm;

        /// <summary>The key by which a monitor is recognised across reboots and port changes.</summary>
        public string Identity
        {
            get
            {
                string sn = !string.IsNullOrEmpty(SerialText) ? SerialText
                          : (SerialNumber != 0 ? SerialNumber.ToString() : null);
                var sb = new StringBuilder();
                sb.Append(string.IsNullOrEmpty(Manufacturer) ? "???" : Manufacturer);
                sb.Append(':');
                sb.Append(string.IsNullOrEmpty(ModelName) ? ProductCode.ToString("X4") : ModelName);
                if (sn != null) { sb.Append(':'); sb.Append(sn); }
                return sb.ToString();
            }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(ModelName) ? (Manufacturer + " " + ProductCode.ToString("X4")) : ModelName; }
        }

        /// <summary>Reads the EDID from the registry by device path like \\?\DISPLAY#GSM5BBF#5&amp;...&amp;UID4353#{guid}</summary>
        public static EdidInfo FromDevicePath(string monitorDevicePath)
        {
            byte[] edid = ReadEdidBytes(monitorDevicePath);
            if (edid == null) return null;
            return Parse(edid);
        }

        public static byte[] ReadEdidBytes(string monitorDevicePath)
        {
            if (string.IsNullOrEmpty(monitorDevicePath)) return null;
            string[] parts = monitorDevicePath.Split('#');
            if (parts.Length < 3) return null;
            string hardwareId = parts[1];
            string instanceId = parts[2];

            string key = @"SYSTEM\CurrentControlSet\Enum\DISPLAY\" + hardwareId + @"\" + instanceId + @"\Device Parameters";
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(key, false))
                {
                    if (k == null) return null;
                    byte[] edid = k.GetValue("EDID") as byte[];
                    if (edid == null || edid.Length < 128) return null;
                    return edid;
                }
            }
            catch { return null; }
        }

        public static EdidInfo Parse(byte[] e)
        {
            if (e == null || e.Length < 128) return null;
            var info = new EdidInfo();

            int mfg = (e[8] << 8) | e[9];
            char c1 = (char)(((mfg >> 10) & 0x1F) + 'A' - 1);
            char c2 = (char)(((mfg >> 5) & 0x1F) + 'A' - 1);
            char c3 = (char)((mfg & 0x1F) + 'A' - 1);
            info.Manufacturer = new string(new[] { c1, c2, c3 });

            info.ProductCode = (ushort)(e[10] | (e[11] << 8));
            info.SerialNumber = (uint)(e[12] | (e[13] << 8) | (e[14] << 16) | (e[15] << 24));
            info.WidthMm = e[21] * 10;
            info.HeightMm = e[22] * 10;

            int[] offsets = new[] { 54, 72, 90, 108 };
            foreach (int off in offsets)
            {
                if (off + 18 > e.Length) break;
                bool isText = e[off] == 0 && e[off + 1] == 0 && e[off + 2] == 0;
                if (isText)
                {
                    byte tag = e[off + 3];
                    if (tag == 0xFF) info.SerialText = ReadText(e, off + 5);
                    else if (tag == 0xFC) info.ModelName = ReadText(e, off + 5);
                }
                else if (info.PreferredWidth == 0)
                {
                    // detailed timing: preferred resolution
                    int hActive = e[off + 2] | ((e[off + 4] & 0xF0) << 4);
                    int vActive = e[off + 5] | ((e[off + 7] & 0xF0) << 4);
                    if (hActive > 0 && vActive > 0) { info.PreferredWidth = hActive; info.PreferredHeight = vActive; }
                }
            }
            return info;
        }

        private static string ReadText(byte[] e, int start)
        {
            var sb = new StringBuilder();
            for (int i = start; i < start + 13 && i < e.Length; i++)
            {
                byte b = e[i];
                if (b == 0x0A || b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString().Trim();
        }
    }
}
