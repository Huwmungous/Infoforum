namespace IFAuthenticator.Controllers
{
    public class UserPass
    {
        public string User { get; set; } = string.Empty;

        public string Pass { get; set; } = string.Empty;

        public DateTime Expires { get; set; } = DateTime.Now.AddMinutes(30);
    }
}