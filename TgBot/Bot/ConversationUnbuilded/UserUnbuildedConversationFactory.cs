using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TgBot.Bot.ConversationVersion;

namespace TgBot.Bot.ConversationUnbuilded
{
    public class UserUnbuildedConversationFactory : IConversationFactory
    {
        public Type ChatType { get; }
        public Regex FirstMessage { get; }

        public UserUnbuildedConversationFactory()
        {
            this.ChatType = typeof(User);
            this.FirstMessage = new Regex(@"^\s*есть ч[её]\??(?<prj>\s+\S+\s*)?(?<ver>\s+.*?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public Task<IConversation> StartConversation(IMainBot mainBot, ITgUser tgUserInfo, string firstMessage)
        {
            return Task<IConversation>.Factory.StartNew(() =>
            {
                var conversation = new UserUnbuildedConversation(mainBot, tgUserInfo, firstMessage);
                conversation.ProcessConversation();
                return conversation;
            });
        }

        public class UserUnbuildedConversation : IConversation
        {
            private IMainBot MainBot { get; }

            public ITgUser TgUser { get; }
            public string FirstMessage { get; }

            public UserUnbuildedConversation(IMainBot mainBot, ITgUser tgUserInfo, string firstMessage)
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
                var messageRegex = new Regex(@"^\s*есть ч[её]\??(?<prj>\s+\S+\s*)?(?<ver>\s+.*?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
                    if (x.ProjectName == null)
                        return false;
                    
                    var prj = this.MainBot.Projects.Single(xx => xx.Name == x.ProjectName);
                    if (string.IsNullOrEmpty(prj.GitName)) 
                        return false;
                    
                    return (string.IsNullOrWhiteSpace(prjName) && x.RedmineVerbosity != EnumRedmineVerbosity.None) 
                           || (prj.RedmineProjectName == prjName);
                }).ToList();

                var anySend = false;
                foreach (var projectUserSetting in possibleProjects)
                {
                    var prj = this.MainBot.Projects.Single(x => x.Name == projectUserSetting.ProjectName);
                    var comments = this.MainBot.GitService?.GetComments(prj.LastBuildCommitHash)?.Result?.ToList();
                    var message = "Ничего не нашли";
                    if (comments != null)
                    {
                        message = "";
                        if (comments.Any(x => x.Trim() != ""))
                        {
                            foreach (var comt in comments.Where(x => x.Trim() != ""))
                            {
                                message += comt + "\n";
                            }
                        } 
                        else if (comments.Count != 0)
                        {
                            message = "Чёт есть";
                        }

                        message = "Проект " + prj.Name + "\n\n" + message;

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