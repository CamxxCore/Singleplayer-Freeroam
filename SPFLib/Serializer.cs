using System.Runtime.Serialization;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace SPFLib
{
    public class Serializer
    {
        public static byte[] SerializeObject<T>(T objectToSerialize)
        //same as above, but should technically work anyway
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream memStr = new MemoryStream();
            bf.Serialize(memStr, objectToSerialize);
            memStr.Position = 0;
            //return "";
            return memStr.ToArray();
        }

        public static T DeserializeObject<T>(byte[] dataStream)
        {
            MemoryStream memStr = new MemoryStream(dataStream);
            memStr.Position = 0;
            BinaryFormatter bf = new BinaryFormatter();
            bf.Binder = new VersionFixer();
            return (T)bf.Deserialize(memStr);
        }
    }

    sealed class VersionFixer : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            Type typeToDeserialize = null;

            // For each assemblyName/typeName that you want to deserialize to
            // a different type, set typeToDeserialize to the desired type.
            string assemVer1 = Assembly.GetExecutingAssembly().FullName;
            if (assemblyName != assemVer1)
            {
                // To use a type from a different assembly version, 
                // change the version number.
                // To do this, uncomment the following line of code.
                assemblyName = assemVer1;
                // To use a different type from the same assembly, 
                // change the type name.
            }
            // The following line of code returns the type.
            typeToDeserialize = Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
            return typeToDeserialize;
        }
    }
}
