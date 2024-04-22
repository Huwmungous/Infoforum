namespace IFAuthenticator
{

    public class LdapSettings
    {
        public string Server { get; set; } = Environment.GetEnvironmentVariable("LDAP_SERVER") ?? "localhost";
        public int Port { get; set; } = 389;
        public string Domain { get; set; } = "longmanrd.infoforum.co.uk";
        public bool UseSSL { get; set; } = false;
    }
} 