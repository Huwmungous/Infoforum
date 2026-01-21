namespace ConfigWebService.Models
{
    public class UsrSvcSettings
    {
        public int Idx { get; set; }
        public string Realm { get; set; } = null!;
        public string Client { get; set; } = null!;
        public string UserConfig { get; set; } = null!;
        public string ServiceConfig { get; set; } = null!;
        public string PatientConfig { get; set; } = null!;

        public string Jsonb { get; set; } = null!;
    }
}
