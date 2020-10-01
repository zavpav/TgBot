using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TgBot.Bot.ConversationVersion
{
    /// <summary> Групповой диалог получения информации по версии </summary>
    public class GroupVersionConversationFactory : IConversationFactory
    {
        public Type ChatType { get; }
        
        public Regex FirstMessage { get; }

        public GroupVersionConversationFactory()
        {
            this.ChatType = typeof(Group);
            this.FirstMessage = new Regex(@"^\s*версия(?<prj>\s+\S+\s*)?(?<ver>\s+.*?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public Task<IConversation> StartConversation(IMainBot mainBot, ITgUser tgUserInfo, string firstMessage)
        {
            var reMatch = this.FirstMessage.Match(firstMessage);
            var prjName = tgUserInfo.Name;
            if (reMatch.Groups["prj"].Success)
                prjName = reMatch.Groups["prj"].Value.Trim();
            string ver = null;
            if (reMatch.Groups["ver"].Success)
                ver = reMatch.Groups["ver"].Value.Trim();

            var prj = mainBot.Projects.SingleOrDefault(x => x.RedmineProjectName != null && x.RedmineProjectName.ToLower() == prjName.ToLower());
            if (prj != null)
            {
                return Task<IConversation>.Factory.StartNew(() =>
                {
                    var conversation = new SimpleMessageConversation(mainBot, tgUserInfo);
                    var versionMessage = ConversationHelper.GenerateVersionInfo(prj, mainBot, prj.VersionCurrent).Result;
                    var msgTask = conversation.ProcessMessage(versionMessage);
                    if (!msgTask.IsCompleted)
                        msgTask.Wait();
                    return conversation;
                });
            }
            else
            {
                return Task<IConversation>.Factory.StartNew(() =>
                {
                    var conversation = new SimpleMessageConversation(mainBot, tgUserInfo);
                    var msgTask = conversation.ProcessMessage("Проект неизвестен");
                    if (!msgTask.IsCompleted)
                        msgTask.Wait();
                    return conversation;
                });
            }
        }

    }
}