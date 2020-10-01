using System.Collections.Generic;

namespace TgBot.AppConfiguration
{
    public class AppConfiguration
    {
        public ServiceConfiguration Services { get; set; }

        public List<ProjectConfiguration> Projects { get; set; }
    }
}