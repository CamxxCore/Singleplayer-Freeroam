using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPFLib.Enums;

namespace SPFLib.Types
{
    public class AIEvent
    {
        public int ID { get; set; }
        public AIEventType EventType { get; set; }

        public AIEvent()
        { }

        public AIEvent(int id, AIEventType eventType)
        { }
    }
}
