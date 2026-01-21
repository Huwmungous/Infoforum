namespace KeycloakWebService.Models
{
    public class KeycloakRealm
    {
        public string? Id { get; set; } = null!;
        public string Realm { get; set; } = null!;
        public bool Enabled { get; set; }
    }

}
