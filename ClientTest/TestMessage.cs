using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.IO;

using NTCPMSG.Serialize;
using NTCPMSG;

namespace ClientTest
{
    public class TestMessageSerializer : ISerialize<TestMessage>
    {

        #region ISerialize<TestMessage> Members

        public byte[] GetBytes(ref TestMessage obj)
        {
            if (obj == null)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream();

            SimpleBinSerializer.Write(ms, obj.Id);
            SimpleBinSerializer.Write(ms, obj.Name);
            SimpleBinSerializer.Write(ms, obj.Data);

            return ms.ToArray();
        }

        public TestMessage GetObject(byte[] data)
        {
            if (data == null)
            {
                return null;
            }

            TestMessage testMessage = new TestMessage();
            MemoryStream ms = new MemoryStream(data);
            testMessage.Id = SimpleBinSerializer.ToInt32(ms);
            testMessage.Name = SimpleBinSerializer.ToString(ms);
            testMessage.Data = SimpleBinSerializer.ToData(ms);

            return testMessage;
        }

        #endregion
    }

    [Serializable]
    public class TestMessage
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
    }

    public struct StructMessage
    {
        public int Id;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;

        //[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        //public string Url;

        //[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        //public string Site;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U1)]
        public byte[] Data;
    }
}
