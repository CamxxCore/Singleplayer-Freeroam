using SPFClient.Config;

namespace SPFClient
{
    public class Configuration
    {
        string Name = IniHelper.GetConfigSetting("General", "Username", "Unknown Player");
    }
}
