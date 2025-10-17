using Microsoft.Extensions.Configuration;

namespace Server.Utils
{
    public static class Configuration
    {
        public static string GetConnectionString()
        {
            //   string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "env.database");
            //      if (!File.Exists(filePath))
            //          throw new FileNotFoundException($"Database configuration file not found at {filePath}");
            //      return File.ReadAllText(filePath);
            return "Data Source=localhost; Initial Catalog=DeveloperWebSide; User Id=SA; Password=reallyStrongPwd123; TrustServerCertificate=True";
        }
    }
    public static class GlobalConfig
    {
        public static IConfiguration Configuration { get; set; }
    }
}