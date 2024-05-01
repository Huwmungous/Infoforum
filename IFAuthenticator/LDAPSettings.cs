namespace IFAuthenticator
{

    public class LdapSettings
    {
        public string Server { get; set; } = Environment.GetEnvironmentVariable("LDAP_SERVER") ?? "localhost";
        public int Port { get { return UseSSL ? 636 : 389; } }
        public string Domain { get; set; } = Environment.GetEnvironmentVariable("LDAP_DOMAIN") ?? "longmanrd.infoforum.co.uk";
        public bool UseSSL { get; set; } = true;
    }
} 