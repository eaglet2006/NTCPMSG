using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace NTCPMSG
{
    class WinAPI
    {
        [DllImport("kernel32.dll")]
        internal static extern bool SetThreadPriorityBoost(IntPtr hThread,
           bool DisablePriorityBoost);
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        internal static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);
    }
}
