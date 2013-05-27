using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace NTCPMSG.Serialize
{
    public class JsonSerializer : ISerialize
    {
        Type _DataType;

        public JsonSerializer(Type dataType)
        {
            _DataType = dataType; 
        }

        #region ISerialize Members

        public byte[] GetBytes(object obj)
        {
#if Dot40
            if (obj == null)
            {
                return null;
            }

            System.Runtime.Serialization.Json.DataContractJsonSerializer serializer =
                    new System.Runtime.Serialization.Json.DataContractJsonSerializer(_DataType);

            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                serializer.WriteObject(ms, obj);
                return ms.ToArray();
            }
#else
            throw new NotImplementedException();
#endif
        }

        public object GetObject(byte[] data)
        {
#if Dot40
            if (data == null)
            {
                return null;
            }

            using (MemoryStream ms = new MemoryStream(data))
            {
                System.Runtime.Serialization.Json.DataContractJsonSerializer serializer =
                    new System.Runtime.Serialization.Json.DataContractJsonSerializer(_DataType);
                return serializer.ReadObject(ms);
            }
#else
            throw new NotImplementedException();
#endif
        }

        #endregion
    }

    public class JsonSerializer<T> : ISerialize<T>
    {

        #region ISerialize<T> Members

        public byte[] GetBytes(ref T obj)
        {
#if Dot40
            System.Runtime.Serialization.Json.DataContractJsonSerializer serializer =
             new System.Runtime.Serialization.Json.DataContractJsonSerializer(obj.GetType());
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, obj);
                return ms.ToArray();
            }
#else
            throw new NotImplementedException();
#endif
        }

        public T GetObject(byte[] data)
        {
#if Dot40
            using (MemoryStream ms = new MemoryStream(data))
            {
                System.Runtime.Serialization.Json.DataContractJsonSerializer serializer =
                new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T));
                return (T)serializer.ReadObject(ms);
            }
#else
            throw new NotImplementedException();
#endif

        }

        #endregion
    }
}
