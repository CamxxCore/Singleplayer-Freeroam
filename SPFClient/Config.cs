using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPFClient.Config;

namespace SPFClient
{
    public class Configuration
    {
        string Name = INIHelper.GetConfigSetting("General", "Username", "Unknown Player");
    }
}
