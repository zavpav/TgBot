using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TgBot.Bot.ConversationVersion
{
    public class UserVersionConversationFactory : IConversationFactory
    {
        public Type ChatType { get; }
        public Regex FirstMessage { get; }

        public UserVersionConversationFactory()
        {
            this.ChatType = typeof(User);
            this.FirstMessage = new Regex(@"^\s*версия(?<prj>\s+\S+\s*)?(?<ver>\s+.*?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public Task<IConversation> StartConversation(IMainBot mainBot, ITgUser tgUserInfo, string firstMessage)
        {
            return Task<IConversation>.Factory.StartNew(() =>
            {
                var conversation = new UserVersionConversation(mainBot, tgUserInfo, firstMessage);
                conversation.ProcessConversation();
                return conversation;
            });
        }

        public class UserVersionConversation : IConversation
        {
            private IMainBot MainBot { get; }

            public ITgUser TgUser { get; }
            public string FirstMessage { get; }

            public UserVersionConversation(IMainBot mainBot, ITgUser tgUserInfo, string firstMessage)
            {
                this.MainBot = mainBot;
                this.TgUser = tgUserInfo;
                this.FirstMessage = firstMessage;
            }

            public Task ProcessMessage(string message)
            {
                this.IsActive = false;
                return Task.CompletedTask;
            }

            public bool IsActive { get; set; }

            public void ProcessConversation()
            {
                var messageRegex = new Regex(@"^\s*версия(?<prj>\s+\S+\s*)?(?<ver>\s+.*?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var usr = (User)this.TgUser;

                var reMatch = messageRegex.Match(this.FirstMessage);
                string prjName = null;
                if (reMatch.Groups["prj"].Success)
                    prjName = reMatch.Groups["prj"].Value.Trim();
                string ver = null;
                if (reMatch.Groups["ver"].Success)
                    ver = reMatch.Groups["ver"].Value.Trim();

                var possibleProjects = usr.ProjectsSettings.Where(x =>
                {
                    
                    return (string.IsNullOrWhiteSpace(prjName) && x.RedmineVerbosity != EnumRedmineVerbosity.None && x.ProjectName != null)
                        || (x.ProjectName != null && this.MainBot.Projects.Single(xx => xx.Name == x.ProjectName).RedmineProjectName == prjName);
                }).ToList();

                var anySend = false;
                foreach (var projectUserSetting in possibleProjects)
                {
                    var prj = this.MainBot.Projects.SingleOrDefault(x => x.Name == projectUserSetting.ProjectName);
                    if (prj != null && !string.IsNullOrEmpty(prj.RedmineProjectName))
                    {
                        var message = ConversationHelper.GenerateVersionInfo(prj, this.MainBot, ver ?? prj.VersionCurrent).Result;
                        this.MainBot.DirectSendMessage(this.TgUser, message);
                        anySend = true;
                    }
                }

                if (!anySend)
                    this.MainBot.DirectSendMessage(this.TgUser, "Нет информации по рабочим проектам");

                this.MainBot.FinishDialog(this.TgUser);
            }
        }
    }
}