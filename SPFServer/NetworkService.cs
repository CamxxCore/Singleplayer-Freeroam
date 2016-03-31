using System;
using System.ServiceModel;
using System.Linq;
using ASUPService;
using System.Threading.Tasks;

namespace SPFServer
{
    public class NetworkService
    {
        private static ASUPServiceClient service;
        public static ASUPServiceClient Service { get { return service; } }

        private const string EndpointURI = "https://camx.me/asupstatsvc/asupservice.svc";
        public static bool Initialized { get; private set; } = false;

        /// <summary>
        /// Establish connection to the master server endpoint.
        /// </summary>
        public void Initialize()
        {
            var binding = new WSHttpBinding(SecurityMode.Transport);

            binding.CloseTimeout = TimeSpan.Parse("00:01:00");
            binding.OpenTimeout = TimeSpan.Parse("00:01:00");
            binding.ReceiveTimeout = TimeSpan.Parse("00:15:00");
            binding.SendTimeout = TimeSpan.Parse("00:15:00");
            binding.MaxBufferPoolSize = 2147483647;
            binding.MaxReceivedMessageSize = 2147483647;
            binding.ReaderQuotas.MaxDepth = 32;
            binding.ReaderQuotas.MaxStringContentLength = 8192;
            binding.ReaderQuotas.MaxArrayLength = 16384;
            binding.ReaderQuotas.MaxBytesPerRead = 4096;
            binding.ReaderQuotas.MaxNameTableCharCount = 16384;

            service = new ASUPServiceClient(binding, new EndpointAddress(EndpointURI));

            if (Service_IsConnected())
                Initialized = true;
        }

        /// <summary>
        /// Close connection to service endpoint.
        /// </summary>
        public void Close()
        {
            try
            {
                service.Close();
            }

            catch (EndpointNotFoundException)
            {
                service.Abort();
            }
            catch (Exception)
            {
                service.Abort();
            }

            Initialized = false;
        }

        internal int AnnounceSession(string sessionName)
        {
            try
            {
                int sessionID = service.AnnounceSession(sessionName);
                return sessionID;
            }

            catch (EndpointNotFoundException)
            {
                Logger.Log("Could not connect to the master server endpoint. Ensure you are connected to the internet and have allowed the program through your firewall.");
            }

            catch (Exception ex)
            {
                Logger.Log("Failed initializing network service.");
            }

            return -1;
        }

        /// <summary>
        /// Update player experience based on the current value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="showUI"></param>
        public async void UpdatePlayerExp(int uid, int value)
        {
            if (Initialized)
            {
                try
                {
                    await service.UpdatePlayerStatAsync(uid, "totalExp", value);
                    return;
                }

                catch (EndpointNotFoundException)
                {
                }

                catch (Exception)
                {
                }

                Close();
            }
        }

        public async void UpdatePlayerRank(int uid, int value)
        {
            if (!Initialized) return;

            try
            {
                await service.UpdatePlayerStatAsync(uid, "currentRank", value);
                return;
            }

            catch (EndpointNotFoundException)
            {
            }

            catch (Exception)
            {
            }

            Close();
        }

        public async void SetPlayerRank(int uid, int value)
        {
            if (!Initialized) return;

            try
            {
                await service.SetPlayerStatAsync(uid, "currentRank", value);
                return;
            }

            catch (EndpointNotFoundException)
            {
            }

            catch (Exception)
            {
            }

            Close();
        }

        public async void UpdatePlayerKills(int uid, int value)
        {
            if (!Initialized) return;

            try
            {
                await service.UpdatePlayerStatAsync(uid, "totalKills", value);
                return;
            }

            catch (EndpointNotFoundException)
            {
            }

            catch (Exception)
            {
            }

            Close();
        }

        public async void UpdatePlayerDeaths(int uid, int value)
        {
            if (!Initialized) return;

            try
            {
                await service.UpdatePlayerStatAsync(uid, "totalDeaths", value);
                return;
            }

            catch (EndpointNotFoundException)
            {
            }

            catch (Exception)
            {
            }

            Close();
        }

        public int GetPlayerStat(int uid, string statName)
        {
            return service.GetPlayerStatAsync(uid, statName).Result;
        }

        public UserInfo[] GetUserStatList()
        {
            if (!Initialized) return new UserInfo[0];
            try
            {
                return service.GetUserStatTable()
                    .OrderBy(x => x.TotalExp)
                    .ThenBy(x => x.TotalKills)
                    .Reverse()
                    .ToArray();
            }

            catch
            {
                return null;
            }
        }

        public ActiveSession[] GetSessionList()
        {
            if (!Initialized) return new ActiveSession[0];
            try
            {
                return service.GetSessionList();
            }

            catch
            {
                return null;
            }
        }

        private bool Service_IsConnected()
        {
            try
            {
                service.TryConnect();
                return true;
            }

            catch (EndpointNotFoundException)
            {
            }

            catch (Exception)
            {
            }

            Close();
            return false;
        }

        public void SendHeartbeat(SessionUpdate update)
        {
            service.SendHeartbeat(update);
        }
    }
}
