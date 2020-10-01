using JetBrains.Annotations;

namespace TgBot.AppConfiguration
{
    public class JenkinsConfiguration
    {
        [CanBeNull]
        public string Address { get; set; }

        [CanBeNull]
        public NetworkCredentialConfiguration NetworkCredential { get; set; }

    }
}