using System;
using System.Collections.Generic;
using System.Text;

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

            System.Web.Script.Serialization.JavaScriptSerializer serializer =
                new System.Web.Script.Serialization.JavaScriptSerializer();
            
            string sJSON = serializer.Serialize(obj);

            return Encoding.UTF8.GetBytes(sJSON);
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

            System.Web.Script.Serialization.JavaScriptSerializer serializer =
               new System.Web.Script.Serialization.JavaScriptSerializer();

            return serializer.Deserialize(Encoding.UTF8.GetString(data), _DataType);
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

            System.Web.Script.Serialization.JavaScriptSerializer serializer =
                new System.Web.Script.Serialization.JavaScriptSerializer();

            string sJSON = serializer.Serialize(obj);

            return Encoding.UTF8.GetBytes(sJSON);
#else
            throw new NotImplementedException();
#endif
        }

        public T GetObject(byte[] data)
        {
#if Dot40
            System.Web.Script.Serialization.JavaScriptSerializer serializer =
                      new System.Web.Script.Serialization.JavaScriptSerializer();

            return serializer.Deserialize<T>(Encoding.UTF8.GetString(data));
#else
            throw new NotImplementedException();
#endif

        }

        #endregion
    }
}
