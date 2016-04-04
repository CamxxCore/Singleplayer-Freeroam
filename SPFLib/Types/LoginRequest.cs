namespace SPFLib.Types
{
    public class LoginRequest
    {
        public int Revision { get; set; }
        public int UID { get; set; }
        public string Username { get; set; }
 
        public LoginRequest()
        {
        }

        public LoginRequest(int revision, int uid, string username)
        {
            Revision = revision;
            UID = uid;
            Username = username;
 
        }
    }
}
