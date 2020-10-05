using JetBrains.Annotations;

namespace TgBot.AppConfiguration
{
    public class GitConfiguration
    {

        [CanBeNull]
        public string Name { get; set; }
        
        [CanBeNull]
        public string Bin { get; set; }

        [CanBeNull]
        public string Dir { get; set; }

        [CanBeNull]
        public string Repository { get; set; }
    }
}