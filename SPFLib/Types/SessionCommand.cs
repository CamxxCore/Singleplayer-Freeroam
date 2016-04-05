using SPFLib.Enums;

namespace SPFLib.Types
{
    public class SessionCommand
    {
        public CommandType Command { get; set; }

        public SessionCommand(CommandType type)
        {
            Command = type;
        }

        public SessionCommand()
        {
        }
    }
}
