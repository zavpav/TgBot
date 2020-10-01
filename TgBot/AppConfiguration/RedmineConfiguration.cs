using JetBrains.Annotations;

namespace TgBot.AppConfiguration
{
    public class RedmineConfiguration
    {
        [CanBeNull]
        public string Address { get; set; }

        [CanBeNull]
        public NetworkCredentialConfiguration NetworkCredential { get; set; }
    }
}