using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer
{
    public sealed class ServerVar<T> 
    {
        public readonly string Name;
        public T Value { get; private set; }

        public ServerVar(string name, T val)
        {
            Name = name;
            Value = val;
        }

        public void SetValue(T value)
        {
            Value = value;
        }
    }
}
