using System;
using System.Collections.Generic;
using System.IO;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class SessionState
    {
        public DateTime Timestamp { get; set; }
        public ClientState[] Clients { get; set; }

        public SessionState()
        {
            Timestamp = default(DateTime);
        }

        public SessionState(byte[] data)
        {
            int seekIndex = 0;

            seekIndex += 1;

            Timestamp = new DateTime(BitConverter.ToInt64(data, seekIndex));

            seekIndex += 8;

            var clientCount = BitConverter.ToInt16(data, seekIndex);

            seekIndex += 2;

            byte[] buffer;
            var clients = new List<ClientState>();

            for (int i = 0; i < clientCount; i++)
            {
                bool bInVehicle = BitConverter.ToBoolean(data, seekIndex + 5);

                bool bSendName = BitConverter.ToBoolean(data, seekIndex + 6);

                buffer = new byte[bInVehicle ? bSendName ? (80 + 32) : 80 : bSendName ? (75 + 32) : 75];

                Array.Copy(data, seekIndex, buffer, 0, buffer.Length);
                clients.Add(new ClientState(buffer));
                seekIndex += buffer.Length;
            }

            Clients = clients.ToArray();
        }

        public byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((byte)NetMessage.SessionUpdate);
                    writer.Write((long)Timestamp.Ticks);
                    writer.Write((short)Clients.Length);

                    foreach (var client in Clients)
                    {
                        if (client != null)
                        writer.Write(client.ToByteArray());
                    }
                }

                return stream.ToArray();
            }
        }
    }
}
