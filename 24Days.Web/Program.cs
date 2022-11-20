using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace _Days.Web
{
    public class Program
    {
        public static void Main(string[] args)
         => CreateHostBuilder(args)
             .Build()
             .Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureUmbracoDefaults()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStaticWebAssets();
                    webBuilder.UseStartup<Startup>();
                });
    }
}
