using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer.Scripting
{
    public abstract class ScriptBase
    {
        public string Name { get { return GetType().FullName; } }

        internal bool running;

    }
}
