using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPFServer.Types;

namespace SPFServer.Types
{
    public sealed class ServerVarCollection<T> 
    {
        private ServerVar<T>[] serverVars;

        public ServerVarCollection(params ServerVar<T>[] vars)
        {
            serverVars = vars;
        }

        public T GetVar<T>(string name)
        {
            for (int i = 0; i < serverVars.Length; i++)
            {
                if (serverVars[i].Name == name) return (serverVars[i] as ServerVar<T>).Value;
            }

            return default(T);
        }
    }
}
