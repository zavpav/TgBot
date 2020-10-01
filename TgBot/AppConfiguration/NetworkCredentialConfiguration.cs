using JetBrains.Annotations;

namespace TgBot.AppConfiguration
{
    public class NetworkCredentialConfiguration
    {
        [CanBeNull]
        public string Name { get; set; }
        
        [CanBeNull]
        public string Password { get; set; }
    }
}