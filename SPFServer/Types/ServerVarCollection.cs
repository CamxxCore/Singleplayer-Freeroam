using System.Collections.Generic;

namespace SPFServer.Types
{
    public sealed class ServerVarCollection<T> : Dictionary<string, T>
    {
        public bool TryGetVar(string name, out T var)
        {
            if (TryGetValue(name, out var))
                return true;
            else
            {
                var = default(T);
                return false;
            }
        }

        public void SetVar(string name, T val)
        {
            this[name] = val;
        }
    }
}
