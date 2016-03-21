using SPFLib.Enums;

namespace SPFLib.Types
{
    public class SessionCommand
    {
        public int UID { get; set; }
        public string Name { get; set; }
        public CommandType Command { get; set; }

        public SessionCommand()
        {
        }
    }
}
