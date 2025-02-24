namespace IFAuthenticator
{

    public class LdapSettings
    {
        public string Server { get; set; } = Environment.GetEnvironmentVariable("LDAP_SERVER") ?? "localhost";
        public int Port { get { return UseSSL ? 636 : 389; } }
        public string Domain { get; set; } = Environment.GetEnvironmentVariable("LDAP_DOMAIN") ?? "longmanrd.infoforum.co.uk";

        public string SearchBase
        {
            get
            {
                return ConvertDomainToSearchBase(Domain);
            }
        }

        public bool UseSSL { get; set; } = true;

        public static string ConvertDomainToSearchBase(string domain)
        {
            ReadOnlySpan<char> domainSpan = domain;
            var sb = new System.Text.StringBuilder();

            int start = 0;
            bool isFirst = true;

            // Iterate over the domain span to construct the search base
            for (int i = 0; i < domainSpan.Length; i++)
            {
                if (domainSpan[i] == '.' || i == domainSpan.Length - 1) // Check for period or end of the string
                {
                    if (!isFirst)
                        sb.Append(',');

                    sb.Append("DC=");

                    int length = (domainSpan[i] == '.') ? i - start : i - start + 1; // Include the last part if it's the end of the string
                    sb.Append(domainSpan.Slice(start, length));

                    start = i + 1; // Move start past the dot
                    isFirst = false;
                }
            }

            return sb.ToString();
        }


    }
} 