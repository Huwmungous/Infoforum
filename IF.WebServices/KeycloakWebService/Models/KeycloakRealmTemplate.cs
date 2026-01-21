using KeycloakWebService.Models;

public class KeycloakRealmTemplate
{
    public string RealmName { get; set; } = "";
    public List<KeycloakClient> DefaultClients { get; set; } = new();
    public List<KeycloakGroup> DefaultGroups { get; set; } = new();
    //public List<KeycloakRole> DefaultRoles { get; set; } = new();
}