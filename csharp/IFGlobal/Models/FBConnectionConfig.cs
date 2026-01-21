namespace IFGlobal.Models;

public class FBConnectionConfig
{
    public bool RequiresRelay { get; set; } = false;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3050;
    public string Database { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Charset { get; set; } = "UTF8";
    public int ServerType { get; set; } = 0; // 0 = default, 1 = embedded

    public string Role { get; set; } = "RDB$USER";

    public override string ToString()
    {
        return $"DataSource={Host};Port={Port};Database={Database};User={UserName};Password={Password};Charset={Charset};ServerType={ServerType};Role={Role}";
    }
}
