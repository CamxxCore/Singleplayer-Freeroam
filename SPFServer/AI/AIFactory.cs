using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPFServer.AI
{
    public sealed class AIFactory
    {
        Session.SessionServer serverRef;

        public AIFactory(Session.SessionServer server)
        {
            serverRef = server;
        }


    }
}
