using IFGlobal.WebServices;

namespace ChitterChatterDistribution;

public class Program
{
    public static async Task Main(string[] args)
    {
        var options = new ServiceFactoryOptions
        {
            ServiceName = "ChitterChatterDistribution",
            ServiceDescription = "ChitterChatter Download Server",
            PortName = "chitterchatterdistribution",
            DefaultPort = 5004,
            AppDomain = "Infoforum",
            RequireAuthentication = true,
            EnableSwagger = false,
            ConfigureServices = (services, config) =>
            {
                services.AddRazorPages();
                services.Configure<DistributionOptions>(opts =>
                {
                    opts.DistributionPath = config["DistributionPath"] 
                        ?? Path.Combine(AppContext.BaseDirectory, "dist");
                });
            },
            ConfigureApp = (app, env) =>
            {
                app.UseStaticFiles();
                app.MapRazorPages();
            }
        };

        await ServiceFactory.RunAsync(args, options);
    }
}

public class DistributionOptions
{
    public string DistributionPath { get; set; } = "dist";
}
