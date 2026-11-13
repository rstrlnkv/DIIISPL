// DIIISPL — monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.

using System;
using System.Runtime.InteropServices;

namespace DIIISPL
{
    internal static class Native
    {
        // ---- QueryDisplayConfig / SetDisplayConfig ----
        public const uint QDC_ALL_PATHS = 0x00000001;
        public const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

        public const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        public const uint SDC_APPLY = 0x00000080;
        public const uint SDC_NO_OPTIMIZATION = 0x00000100;
        public const uint SDC_SAVE_TO_DATABASE = 0x00000200;
        public const uint SDC_ALLOW_CHANGES = 0x00000400;
        public const uint SDC_FORCE_MODE_ENUMERATION = 0x00001000;

        public const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
        public const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;

        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4;

        public const uint DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE = 1;
        public const uint DISPLAYCONFIG_MODE_INFO_TYPE_TARGET = 2;

        public const uint DISPLAYCONFIG_ROTATION_IDENTITY = 1;
        public const uint DISPLAYCONFIG_ROTATION_ROTATE90 = 2;
        public const uint DISPLAYCONFIG_ROTATION_ROTATE180 = 3;
        public const uint DISPLAYCONFIG_ROTATION_ROTATE270 = 4;

        public const uint DISPLAYCONFIG_SCALING_IDENTITY = 1;
        public const uint DISPLAYCONFIG_SCALING_STRETCHED = 3;
        public const uint DISPLAYCONFIG_SCALING_PREFERRED = 128;

        public const uint DISPLAYCONFIG_SCANLINE_ORDERING_PROGRESSIVE = 1;
        public const uint DISPLAYCONFIG_PIXELFORMAT_32BPP = 4;

        // ---- structures ----
        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
            public ulong Key { get { return ((ulong)(uint)HighPart << 32) | LowPart; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
            public uint targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_2DREGION { public uint cx; public uint cy; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public DISPLAYCONFIG_2DREGION activeSize;
            public DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINTL { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECTL { public int left; public int top; public int right; public int bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
        {
            public POINTL PathSourceSize;
            public RECTL DesktopImageRegion;
            public RECTL DesktopImageClip;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)] public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(0)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
            [FieldOffset(0)] public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO
        {
            public uint infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_ADAPTER_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string adapterDevicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags;
            public uint outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        // ---- advanced colour (HDR) and preferred mode ----
        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE = 3;
        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
        public const uint DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10;
        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11;

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_TARGET_PREFERRED_MODE
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint width;
            public uint height;
            public DISPLAYCONFIG_TARGET_MODE targetMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;            // bits: 0 supported, 1 enabled, 2 wideColorEnforced, 3 forceDisabled
            public uint colorEncoding;
            public uint bitsPerColorChannel;

            public bool Supported { get { return (value & 0x1) != 0; } }
            public bool Enabled { get { return (value & 0x2) != 0; } }
            public bool ForceDisabled { get { return (value & 0x8) != 0; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;            // bit 0: enableAdvancedColor
        }

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_PREFERRED_MODE requestPacket);

        // Windows 11 24H2 separated advanced colour from HDR: the old advancedColorEnabled stays
        // on while the display runs in a wide colour gamut, and HDR proper
        // moved to separate fields of the new packet and a separate set call.
        public const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2 = 15;
        public const uint DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE = 16;

        public const uint DISPLAYCONFIG_ADVANCED_COLOR_MODE_HDR = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;                 // 0 acSupported, 1 acActive, 3 byPolicy,
                                               // 4 hdrSupported, 5 hdrUserEnabled, 6 wcgSupported, 7 wcgUserEnabled
            public uint colorEncoding;
            public uint bitsPerColorChannel;
            public uint activeColorMode;       // 2 — HDR is actually active now

            public bool HdrSupported { get { return (value & 0x10) != 0; } }
            public bool HdrUserEnabled { get { return (value & 0x20) != 0; } }
            public bool LimitedByPolicy { get { return (value & 0x08) != 0; } }
            public bool HdrActive { get { return activeColorMode == DISPLAYCONFIG_ADVANCED_COLOR_MODE_HDR; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_SET_HDR_STATE
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint value;                 // bit 0: enableHdr
        }

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_HDR_STATE setPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE setPacket);

        // ---- GDI mode enumeration ----
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const uint DM_BITSPERPEL = 0x00040000;
        public const uint DM_PELSWIDTH = 0x00080000;
        public const uint DM_PELSHEIGHT = 0x00100000;
        public const uint DM_DISPLAYFREQUENCY = 0x00400000;
        public const uint DM_POSITION = 0x00000020;
        public const uint DM_DISPLAYORIENTATION = 0x00000080;

        public const uint CDS_UPDATEREGISTRY = 0x00000001;
        public const uint CDS_SET_PRIMARY = 0x00000010;
        public const uint CDS_NORESET = 0x10000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        // ---- imports ----
        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(uint flags,
            ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        public static extern int SetDisplayConfig(uint numPathArrayElements, [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
            uint numModeInfoArrayElements, [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, uint flags);

        [DllImport("user32.dll", EntryPoint = "SetDisplayConfig")]
        public static extern int SetDisplayTopology(uint numPathArrayElements, IntPtr pathArray,
            uint numModeInfoArrayElements, IntPtr modeInfoArray, uint flags);

        public const uint SDC_TOPOLOGY_CLONE = 0x00000002;
        public const uint SDC_TOPOLOGY_EXTEND = 0x00000004;

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_ADAPTER_NAME requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        /// <summary>
        /// Loads an icon of the required size from the program's own resources. This way the icon
        /// is stored in the file once: the Windows resource is needed by Explorer and the taskbar,
        /// and the notification-area icon takes it from there too.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadImage(IntPtr instance, string name, uint type,
                                              int cx, int cy, uint load);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string name);

        public const uint IMAGE_ICON = 1;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId);

        public const int WM_HOTKEY = 0x0312;
        public const int WM_DISPLAYCHANGE = 0x007E;
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);

        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder buffer, int max);

        /// <summary>
        /// Finds a window by class. Needed where EnumWindows is unreliable: while Start,
        /// search or Task View is open, shell windows appear and disappear, enumeration
        /// breaks off halfway and the taskbar simply does not get into it.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // ---- DWM (Fluent: Mica + dark title bar) ----
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const int DWMSBT_MAINWINDOW = 2;   // Mica
        public const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public const int WTA_NONCLIENT = 1;
        public const uint WTNCA_NODRAWCAPTION = 1;
        public const uint WTNCA_NODRAWICON = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct WTA_OPTIONS { public uint Flags, Mask; }

        [DllImport("uxtheme.dll")]
        public static extern int SetWindowThemeAttribute(IntPtr hwnd, int attr, ref WTA_OPTIONS options, int size);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        /// <summary>
        /// The reason for a failure in plain words.
        ///
        /// The system description of the code does not fit here: the codes SetDisplayConfig
        /// returns are described in the general Windows reference as something from a completely different
        /// area — for 1610 the user got "product configuration data is
        /// corrupt, contact support", which has nothing to do with
        /// monitors.
        /// </summary>
        public static string ErrorText(int code)
        {
            switch (code)
            {
                case 5:    return Text.ErrNoRights;
                case 31:   return Text.ErrDriver;
                case 50:   return Text.ErrHardware;
                case 87:   return Text.ErrCombination;
                case 1610: return Text.ErrImpossible;
                default:   return Text.ErrUnexplained;
            }
        }

        // ------------------------------------------------------------------ file signature
        // Signature integrity check. The certificate thumbprint by itself
        // proves nothing: it can be read from a file with anything appended —
        // the certificate stays the same, but the content is already foreign.

        [StructLayout(LayoutKind.Sequential)]
        public struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }

        public const uint WTD_UI_NONE = 2;
        public const uint WTD_REVOKE_NONE = 0;
        public const uint WTD_CHOICE_FILE = 1;
        public const uint WTD_STATEACTION_VERIFY = 1;
        public const uint WTD_STATEACTION_CLOSE = 2;
        public const uint WTD_SAFER_FLAG = 0x100;

        public static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        /// <summary>"Not enough space" — for QueryDisplayConfig it is a request to ask for the sizes again.</summary>
        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        /// <summary>The signature is intact, but the certificate root is not installed on the system.</summary>
        public const int CERT_E_UNTRUSTEDROOT = unchecked((int)0x800B0109);
        public const int CERT_E_CHAINING = unchecked((int)0x800B010A);
        /// <summary>The certificate has expired. The signature is intact — the calendar simply moved on.</summary>
        public const int CERT_E_EXPIRED = unchecked((int)0x800B0101);
        /// <summary>The file content does not match the signature — the file was changed after signing.</summary>
        public const int TRUST_E_BAD_DIGEST = unchecked((int)0x80096010);
        public const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
        public static extern int WinVerifyTrust(IntPtr hwnd, [In] ref Guid pgActionID, IntPtr pWVTData);
    }
}
