namespace IFGlobal.Config;

public interface IConfigProvider
{
    string ClientId { get; }
    string OpenIdConfig { get; }
    string? LoggerService { get; }
    string Realm { get; }
}
