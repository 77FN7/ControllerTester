using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ControllerTester
{
    class RawController
    {
        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int RID_INPUT = 0x10000003;
        private const int WM_INPUT = 0x00FF;
        private const int RIM_TYPEHID = 2;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTDEVICE
        {
            [MarshalAs(UnmanagedType.U2)]
            public ushort usUsagePage;
            [MarshalAs(UnmanagedType.U2)]
            public ushort usUsage;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTHEADER
        {
            [MarshalAs(UnmanagedType.U4)]
            public uint dwType;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwSize;
            public IntPtr hDevice;
            [MarshalAs(UnmanagedType.U4)]
            public int wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWHID
        {
            [MarshalAs(UnmanagedType.U4)]
            public uint dwSizeHid;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwCount;
            // apparently this part isn't possible
            //[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = sizeof(dwSizeHid), SizeConst = dwCount)]
            //public byte[] bRawData; // size = dwSizeHid * dwCount
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BUTTONSSTR
        {
            [MarshalAs(UnmanagedType.U2)]
            public ushort usButtonFlags;
            [MarshalAs(UnmanagedType.U2)]
            public ushort usButtonData;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct RAWMOUSE
        {
            [MarshalAs(UnmanagedType.U2)]
            [FieldOffset(0)]
            public ushort usFlags;
            [MarshalAs(UnmanagedType.U4)]
            [FieldOffset(4)]
            public uint ulButtons;
            [FieldOffset(4)]
            public BUTTONSSTR buttonsStr;
            [MarshalAs(UnmanagedType.U4)]
            [FieldOffset(8)]
            public uint ulRawButtons;
            [FieldOffset(12)]
            public int lLastX;
            [FieldOffset(16)]
            public int lLastY;
            [MarshalAs(UnmanagedType.U4)]
            [FieldOffset(20)]
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWKEYBOARD
        {
            [MarshalAs(UnmanagedType.U2)]
            public ushort MakeCode;
            [MarshalAs(UnmanagedType.U2)]
            public ushort Flags;
            [MarshalAs(UnmanagedType.U2)]
            public ushort Reserved;
            [MarshalAs(UnmanagedType.U2)]
            public ushort VKey;
            [MarshalAs(UnmanagedType.U4)]
            public uint Message;
            [MarshalAs(UnmanagedType.U4)]
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct RAWINPUT
        {
            [FieldOffset(0)]
            public RAWINPUTHEADER header;
            [FieldOffset(16)]
            public RAWMOUSE mouse;
            [FieldOffset(16)]
            public RAWKEYBOARD keyboard;
            [FieldOffset(16)]
            public RAWHID hid;
        }

        [DllImport("user32.dll")]
        extern static bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices,
                                                   uint uiNumDevices,
                                                   uint cbSize);

        [DllImport("User32.dll")]
        extern static uint GetRawInputData(IntPtr hRawInput,
                                           uint uiCommand,
                                           IntPtr pData,
                                           ref uint pcbSize,
                                           uint cbSizeHeader);


        private Stopwatch stopWatch = new Stopwatch();
        public double stopwatch_freq = 0.0;

        public void RegisterRawInputHID(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x05;
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = hwnd;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0])))
            {
                Debug.WriteLine("RegisterRawInputDevices() Failed");
            }

            //Debug.WriteLine("High Resolution Stopwatch: " + Stopwatch.IsHighResolution + "\n" +
            //                "Stopwatch TS: " + (1e6 / Stopwatch.Frequency).ToString() + " us\n" +
            //                "Stopwatch Hz: " + (Stopwatch.Frequency / 1e6).ToString() + " MHz\n");

            this.stopwatch_freq = 1e3 / Stopwatch.Frequency;
        }

        public void StopWatchReset()
        {
            this.stopWatch.Reset();
            this.stopWatch.Start();
        }

        public delegate void ControllerEventHandler(object RawController, ControllerEvent hideventinfo);
        public ControllerEventHandler hidevent;

        public void ProcessRawInput(Message m)
        {
            if (m.Msg == WM_INPUT)
            {
                uint dwSize = 0;

                GetRawInputData(m.LParam,
                                RID_INPUT, IntPtr.Zero,
                                ref dwSize,
                                (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                
                try
                {
                    if (buffer != IntPtr.Zero &&
                        GetRawInputData(m.LParam,
                                        RID_INPUT,
                                        buffer,
                                        ref dwSize,
                                        (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize)
                    {
                        RAWINPUT raw = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));

                        if (raw.header.dwType == RIM_TYPEHID)
                        {
                            if (hidevent != null)
                            {
                                if (!(raw.hid.dwSizeHid > 1     //Make sure our HID msg size more than 1. In fact the first ushort is irrelevant to us for now
                                && raw.hid.dwCount > 0))    //Check that we have at least one HID msg
                                {
                                    return;
                                }

                                //Allocate a buffer for one HID input
                                byte[] InputReport = new byte[raw.hid.dwSizeHid];

                                //Debug.WriteLine("Raw input contains " + raw.hid.dwCount + " HID input report(s)");

                                //For each HID input report in our raw input
                                for (int i = 0; i < raw.hid.dwCount; i++)
                                {
                                    //Compute the address from which to copy our HID input
                                    int hidInputOffset = 0;
                                    unsafe
                                    {
                                        byte* source = (byte*)buffer;
                                        source += sizeof(RAWINPUTHEADER) + sizeof(RAWHID) + (raw.hid.dwSizeHid * i);
                                        hidInputOffset = (int)source;
                                    }

                                    //Copy HID input into our buffer
                                    Marshal.Copy(new IntPtr(hidInputOffset), InputReport, 0, (int)raw.hid.dwSizeHid);
                                    //
                                    //for (int j = 0; j < InputReport.Length; j++)
                                    //{
                                    //    Debug.WriteLine("Input report " + j.ToString() + ": " + InputReport[j].ToString());
                                    //}
                                    int xval = InputReport[2] * 256 + InputReport[1];
                                    int yval = InputReport[4] * 256 + InputReport[3];
                                    //Debug.WriteLine("Input report: " + yval.ToString());

                                    ControllerEvent hideventinfo = new ControllerEvent(raw.header.hDevice, 0, xval, yval,
                                       stopWatch.ElapsedTicks * 1e3 / Stopwatch.Frequency);
                                    hidevent(this, hideventinfo);
                                    //ProcessInputReport(InputReport);
                                }

                                //Debug.WriteLine("received something: " + raw.header.hDevice.ToString() /*+ raw.hid.bRawData[0].ToString()*/);
                            }
                            //Debug.WriteLine((stopWatch.ElapsedTicks * 1e3 / Stopwatch.Frequency).ToString() + ", " +
                            //                raw.mouse.lLastX.ToString() + ", " +
                            //                raw.mouse.lLastY.ToString());
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
    }
}
