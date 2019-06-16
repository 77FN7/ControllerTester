using System;

namespace ControllerTester
{
    public class ControllerEvent
    {
        public ushort buttonflags;
        public int lastx;
        public int lasty;
        public double ts, lastts;

        public IntPtr hDevice;

        public ControllerEvent(IntPtr hDevice, ushort buttonflags, int lastx, int lasty, double ts)
        {
            this.hDevice = hDevice;
            this.buttonflags = buttonflags;
            this.lastx = lastx;
            this.lasty = lasty;
            this.ts = ts;
        }
    }
}
