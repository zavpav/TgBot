using JetBrains.Annotations;

namespace TgBot.AppConfiguration
{
    public class TgConfiguration
    {
        [CanBeNull]
        public string Key { get; set; }

        [CanBeNull]
        public ProxyConfiguration Proxy { get; set; }

        public class ProxyConfiguration
        {
            [CanBeNull]
            public string Host { get; set; }

            public int Port { get; set; }
            
            [CanBeNull]
            public NetworkCredentialConfiguration NetworkCredential { get; set; }
        }
    }
}