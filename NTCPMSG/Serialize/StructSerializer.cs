﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace NTCPMSG.Serialize
{
    public class StructSerializer<T> : ISerialize<T> where T:struct 
    {
        #region ISerialize<T> Members

        public byte[] GetBytes(ref T obj)
        {
            int len = Marshal.SizeOf(obj);

            IntPtr pnt = Marshal.AllocHGlobal(len);

            try
            {
                Marshal.StructureToPtr(obj, pnt, true);

                byte[] buf = new byte[len];

                Marshal.Copy(pnt, buf, 0, len);

                return buf;
            }
            finally
            {
                Marshal.FreeHGlobal(pnt);
            }
        }

        public T GetObject(byte[] data)
        {
            IntPtr pnt = Marshal.AllocHGlobal(data.Length);

            try
            {
                Marshal.Copy(data, 0, pnt, data.Length);
                return (T)Marshal.PtrToStructure(pnt, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(pnt);
            }
        }

        #endregion
    }
}
