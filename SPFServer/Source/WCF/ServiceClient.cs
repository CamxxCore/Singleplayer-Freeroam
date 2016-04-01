﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SPFServer.WCF
{
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name = "ActiveSession", Namespace = "http://schemas.datacontract.org/2004/07/ASUPService")]
    public partial class ActiveSession : object, System.Runtime.Serialization.IExtensibleDataObject
    {

        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;

        private byte[] AddressField;

        private int ClientCountField;

        private string HostnameField;

        private int LastUpdateField;

        private int MaxClientsField;

        private int ServerIDField;

        public System.Runtime.Serialization.ExtensionDataObject ExtensionData
        {
            get
            {
                return this.extensionDataField;
            }
            set
            {
                this.extensionDataField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public byte[] Address
        {
            get
            {
                return this.AddressField;
            }
            set
            {
                this.AddressField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int ClientCount
        {
            get
            {
                return this.ClientCountField;
            }
            set
            {
                this.ClientCountField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Hostname
        {
            get
            {
                return this.HostnameField;
            }
            set
            {
                this.HostnameField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int LastUpdate
        {
            get
            {
                return this.LastUpdateField;
            }
            set
            {
                this.LastUpdateField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int MaxClients
        {
            get
            {
                return this.MaxClientsField;
            }
            set
            {
                this.MaxClientsField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int ServerID
        {
            get
            {
                return this.ServerIDField;
            }
            set
            {
                this.ServerIDField = value;
            }
        }
    }

    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name = "SessionUpdate", Namespace = "http://schemas.datacontract.org/2004/07/ASUPService")]
    public partial class SessionUpdate : object, System.Runtime.Serialization.IExtensibleDataObject
    {

        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;

        private int ClientCountField;

        private int MaxClientsField;

        private int ServerIDField;

        public System.Runtime.Serialization.ExtensionDataObject ExtensionData
        {
            get
            {
                return this.extensionDataField;
            }
            set
            {
                this.extensionDataField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int ClientCount
        {
            get
            {
                return this.ClientCountField;
            }
            set
            {
                this.ClientCountField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int MaxClients
        {
            get
            {
                return this.MaxClientsField;
            }
            set
            {
                this.MaxClientsField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int ServerID
        {
            get
            {
                return this.ServerIDField;
            }
            set
            {
                this.ServerIDField = value;
            }
        }
    }

    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [System.Runtime.Serialization.DataContractAttribute(Name = "UserInfo", Namespace = "http://schemas.datacontract.org/2004/07/ASUPService")]
    public partial class UserInfo : object, System.Runtime.Serialization.IExtensibleDataObject
    {

        private System.Runtime.Serialization.ExtensionDataObject extensionDataField;

        private int CurrentLevelField;

        private int TotalDeathsField;

        private int TotalExpField;

        private int TotalKillsField;

        private int UIDField;

        private string UsernameField;

        public System.Runtime.Serialization.ExtensionDataObject ExtensionData
        {
            get
            {
                return this.extensionDataField;
            }
            set
            {
                this.extensionDataField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int CurrentLevel
        {
            get
            {
                return this.CurrentLevelField;
            }
            set
            {
                this.CurrentLevelField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int TotalDeaths
        {
            get
            {
                return this.TotalDeathsField;
            }
            set
            {
                this.TotalDeathsField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int TotalExp
        {
            get
            {
                return this.TotalExpField;
            }
            set
            {
                this.TotalExpField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int TotalKills
        {
            get
            {
                return this.TotalKillsField;
            }
            set
            {
                this.TotalKillsField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public int UID
        {
            get
            {
                return this.UIDField;
            }
            set
            {
                this.UIDField = value;
            }
        }

        [System.Runtime.Serialization.DataMemberAttribute()]
        public string Username
        {
            get
            {
                return this.UsernameField;
            }
            set
            {
                this.UsernameField = value;
            }
        }
    }
}


[System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
[System.ServiceModel.ServiceContractAttribute(ConfigurationName = "IASUPService")]
public interface IASUPService
{

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/GetSessionList", ReplyAction = "http://tempuri.org/IASUPService/GetSessionListResponse")]
    SPFServer.WCF.ActiveSession[] GetSessionList();

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/GetSessionList", ReplyAction = "http://tempuri.org/IASUPService/GetSessionListResponse")]
    System.Threading.Tasks.Task<SPFServer.WCF.ActiveSession[]> GetSessionListAsync();

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/AnnounceSession", ReplyAction = "http://tempuri.org/IASUPService/AnnounceSessionResponse")]
    int AnnounceSession(string hostname);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/AnnounceSession", ReplyAction = "http://tempuri.org/IASUPService/AnnounceSessionResponse")]
    System.Threading.Tasks.Task<int> AnnounceSessionAsync(string hostname);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/SendHeartbeat", ReplyAction = "http://tempuri.org/IASUPService/SendHeartbeatResponse")]
    void SendHeartbeat(SPFServer.WCF.SessionUpdate update);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/SendHeartbeat", ReplyAction = "http://tempuri.org/IASUPService/SendHeartbeatResponse")]
    System.Threading.Tasks.Task SendHeartbeatAsync(SPFServer.WCF.SessionUpdate update);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/SendRemoteCommand", ReplyAction = "http://tempuri.org/IASUPService/SendRemoteCommandResponse")]
    void SendRemoteCommand(string cmd);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/SendRemoteCommand", ReplyAction = "http://tempuri.org/IASUPService/SendRemoteCommandResponse")]
    System.Threading.Tasks.Task SendRemoteCommandAsync(string cmd);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/GetPlayerStat", ReplyAction = "http://tempuri.org/IASUPService/GetPlayerStatResponse")]
    int GetPlayerStat(int uid, string statName);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/GetPlayerStat", ReplyAction = "http://tempuri.org/IASUPService/GetPlayerStatResponse")]
    System.Threading.Tasks.Task<int> GetPlayerStatAsync(int uid, string statName);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/SetPlayerStat", ReplyAction = "http://tempuri.org/IASUPService/SetPlayerStatResponse")]
    bool SetPlayerStat(int uid, string statName, int value);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/SetPlayerStat", ReplyAction = "http://tempuri.org/IASUPService/SetPlayerStatResponse")]
    System.Threading.Tasks.Task<bool> SetPlayerStatAsync(int uid, string statName, int value);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/UpdatePlayerStat", ReplyAction = "http://tempuri.org/IASUPService/UpdatePlayerStatResponse")]
    bool UpdatePlayerStat(int uid, string statName, int value);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/UpdatePlayerStat", ReplyAction = "http://tempuri.org/IASUPService/UpdatePlayerStatResponse")]
    System.Threading.Tasks.Task<bool> UpdatePlayerStatAsync(int uid, string statName, int value);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/CreateUser", ReplyAction = "http://tempuri.org/IASUPService/CreateUserResponse")]
    bool CreateUser(int uid, string name);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/CreateUser", ReplyAction = "http://tempuri.org/IASUPService/CreateUserResponse")]
    System.Threading.Tasks.Task<bool> CreateUserAsync(int uid, string name);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/UserExists", ReplyAction = "http://tempuri.org/IASUPService/UserExistsResponse")]
    bool UserExists(int uid);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/UserExists", ReplyAction = "http://tempuri.org/IASUPService/UserExistsResponse")]
    System.Threading.Tasks.Task<bool> UserExistsAsync(int uid);

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/GetUserStatTable", ReplyAction = "http://tempuri.org/IASUPService/GetUserStatTableResponse")]
    SPFServer.WCF.UserInfo[] GetUserStatTable();

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/GetUserStatTable", ReplyAction = "http://tempuri.org/IASUPService/GetUserStatTableResponse")]
    System.Threading.Tasks.Task<SPFServer.WCF.UserInfo[]> GetUserStatTableAsync();

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/TryConnect", ReplyAction = "http://tempuri.org/IASUPService/TryConnectResponse")]
    bool TryConnect();

    [System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IASUPService/TryConnect", ReplyAction = "http://tempuri.org/IASUPService/TryConnectResponse")]
    System.Threading.Tasks.Task<bool> TryConnectAsync();
}

[System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
public interface IASUPServiceChannel : IASUPService, System.ServiceModel.IClientChannel
{
}

[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
public partial class ASUPServiceClient : System.ServiceModel.ClientBase<IASUPService>, IASUPService
{

    public ASUPServiceClient()
    {
    }

    public ASUPServiceClient(string endpointConfigurationName) :
            base(endpointConfigurationName)
    {
    }

    public ASUPServiceClient(string endpointConfigurationName, string remoteAddress) :
            base(endpointConfigurationName, remoteAddress)
    {
    }

    public ASUPServiceClient(string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) :
            base(endpointConfigurationName, remoteAddress)
    {
    }

    public ASUPServiceClient(System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) :
            base(binding, remoteAddress)
    {
    }

    public SPFServer.WCF.ActiveSession[] GetSessionList()
    {
        return base.Channel.GetSessionList();
    }

    public System.Threading.Tasks.Task<SPFServer.WCF.ActiveSession[]> GetSessionListAsync()
    {
        return base.Channel.GetSessionListAsync();
    }

    public int AnnounceSession(string hostname)
    {
        return base.Channel.AnnounceSession(hostname);
    }

    public System.Threading.Tasks.Task<int> AnnounceSessionAsync(string hostname)
    {
        return base.Channel.AnnounceSessionAsync(hostname);
    }

    public void SendHeartbeat(SPFServer.WCF.SessionUpdate update)
    {
        base.Channel.SendHeartbeat(update);
    }

    public System.Threading.Tasks.Task SendHeartbeatAsync(SPFServer.WCF.SessionUpdate update)
    {
        return base.Channel.SendHeartbeatAsync(update);
    }

    public void SendRemoteCommand(string cmd)
    {
        base.Channel.SendRemoteCommand(cmd);
    }

    public System.Threading.Tasks.Task SendRemoteCommandAsync(string cmd)
    {
        return base.Channel.SendRemoteCommandAsync(cmd);
    }

    public int GetPlayerStat(int uid, string statName)
    {
        return base.Channel.GetPlayerStat(uid, statName);
    }

    public System.Threading.Tasks.Task<int> GetPlayerStatAsync(int uid, string statName)
    {
        return base.Channel.GetPlayerStatAsync(uid, statName);
    }

    public bool SetPlayerStat(int uid, string statName, int value)
    {
        return base.Channel.SetPlayerStat(uid, statName, value);
    }

    public System.Threading.Tasks.Task<bool> SetPlayerStatAsync(int uid, string statName, int value)
    {
        return base.Channel.SetPlayerStatAsync(uid, statName, value);
    }

    public bool UpdatePlayerStat(int uid, string statName, int value)
    {
        return base.Channel.UpdatePlayerStat(uid, statName, value);
    }

    public System.Threading.Tasks.Task<bool> UpdatePlayerStatAsync(int uid, string statName, int value)
    {
        return base.Channel.UpdatePlayerStatAsync(uid, statName, value);
    }

    public bool CreateUser(int uid, string name)
    {
        return base.Channel.CreateUser(uid, name);
    }

    public System.Threading.Tasks.Task<bool> CreateUserAsync(int uid, string name)
    {
        return base.Channel.CreateUserAsync(uid, name);
    }

    public bool UserExists(int uid)
    {
        return base.Channel.UserExists(uid);
    }

    public System.Threading.Tasks.Task<bool> UserExistsAsync(int uid)
    {
        return base.Channel.UserExistsAsync(uid);
    }

    public SPFServer.WCF.UserInfo[] GetUserStatTable()
    {
        return base.Channel.GetUserStatTable();
    }

    public System.Threading.Tasks.Task<SPFServer.WCF.UserInfo[]> GetUserStatTableAsync()
    {
        return base.Channel.GetUserStatTableAsync();
    }

    public bool TryConnect()
    {
        return base.Channel.TryConnect();
    }

    public System.Threading.Tasks.Task<bool> TryConnectAsync()
    {
        return base.Channel.TryConnectAsync();
    }
}