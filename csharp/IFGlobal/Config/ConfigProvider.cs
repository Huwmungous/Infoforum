namespace IFGlobal.Config;

public class ConfigProvider : IConfigProvider
{
    public required string ClientId { get; init; }
    public required string OpenIdConfig { get; init; }
    public string? LoggerService { get; init; }
    public required string Realm { get; init; }
}
