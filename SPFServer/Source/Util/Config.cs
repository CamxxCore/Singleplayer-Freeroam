using System.IO;
using SPFServer.Types;

namespace SPFServer
{
    public static class Config
    {
        public static void GetOverrideVarsInt(string configPath, ref ServerVarList<int> vars)
        {
            if (!File.Exists(configPath))
                return;

            var lines = File.ReadAllLines(configPath);

            int varValue = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("setv"))
                    continue;
                var varInfo = lines[i].Split(' ');

                if (varInfo.Length != 3) continue;

                if (vars.ContainsKey(varInfo[1]) && int.TryParse(varInfo[2], out varValue))
                {
                    vars.SetVar(varInfo[1], varValue);
                }
            }
        }

        public static void GetOverrideVarsString(string configPath, ref ServerVarList<string> vars)
        {
            if (!File.Exists(configPath))
                return;

            var lines = File.ReadAllLines(configPath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("set"))
                    continue;
                var varInfo = lines[i].Split(' ');

                if (varInfo.Length != 3) continue;

                if (vars.ContainsKey(varInfo[1]))
                {
                    vars.SetVar(varInfo[1], varInfo[2]);
                }
            }
        }
    }
}
