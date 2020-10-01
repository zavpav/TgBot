using JetBrains.Annotations;
using TgBot.Services;

namespace TgBot.BuildPuller
{
    /// <summary> Пинальщик дженкинса </summary>
    public class JenkinsPuller : IBuildPuller
    {
        [NotNull] 
        public JenkinsService BuildServerService { get; }

        public JenkinsPuller([NotNull] JenkinsService buildServerService)
        {
            this.BuildServerService = buildServerService;
        }

        public string Address { get; set; }

        public void PullNews()
        {
        }
    }
}