namespace IFGlobal.Models;

public class PGConnectionConfig
{
    public bool RequiresRelay { get; set; } = false;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Host={Host};Port={Port};Database={Database};Username={UserName};Password={Password}";
    }
}
