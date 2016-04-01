namespace SPFLib.Types
{
    public class LoginRequest
    {
        public int UID { get; set; }
        public string Username { get; set; }

        public LoginRequest()
        {
        }

        public LoginRequest(int uid, string username)
        {
            UID = uid;
            Username = username;
        }
    }
}
