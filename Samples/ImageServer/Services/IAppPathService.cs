namespace ImageServer.Services
{
    public interface IAppConfigService
    {
         AppConfig Config{get;set;}
    }

    public class AppConfigService : IAppConfigService
    {
        public AppConfig Config {get;set;}
    }
}