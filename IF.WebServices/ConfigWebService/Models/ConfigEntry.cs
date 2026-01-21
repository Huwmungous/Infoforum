using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConfigWebService.Entities;

[Table("usr_svc_settings", Schema = "public")]
public class ConfigEntry
{
    [Key]
    [Column("idx")]
    public int Idx { get; set; }

    [Column("realm")]
    public string Realm { get; set; } = null!;

    [Column("client")]
    public string Client { get; set; } = null!;

    [Column("user_config")]
    public string UserConfig { get; set; } = null!;

    [Column("service_config")]
    public string ServiceConfig { get; set; } = null!;

    [Column("patient_config")]
    public string PatientConfig { get; set; } = null!;

    [Column("jsonb")]
    public string JsonB { get; set; } = null!;
}
