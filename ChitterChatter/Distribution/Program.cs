using IFGlobal.Configuration;
using IFGlobal.WebServices;

namespace ChitterChatterDistribution;

public class Program
{
    public static async Task Main(string[] args)
    {
        var options = new ServiceFactoryOptions
        {
            ServiceName = "ChitterChatterDistribution",
            Description = "Infoforum Downloads API",
            UseAuthentication = true,
            AuthType = AuthType.Service,
            ConfigureServices = (services, context) =>
            {
                services.Configure<DistributionOptions>(opts =>
                {
                    opts.DistributionPath = context.Configuration["DistributionPath"]
                        ?? Path.Combine(AppContext.BaseDirectory, "dist");
                });
            },
            ConfigurePipeline = (app, context) =>
            {
                app.UseStaticFiles();
            }
        };

        var app = await ServiceFactory.CreateAsync(options);
        await app.RunAsync();
    }
}

public class DistributionOptions
{
    public string DistributionPath { get; set; } = "dist";
}
