using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TgBot.Bot.StatLongTimeConversation
{
    public class UserStatLongTimeConversationFactory : IConversationFactory
    {
        public UserStatLongTimeConversationFactory()
        {
            ChatType = typeof(User);
            FirstMessage =  new Regex(@"^\s*стат(?<prj>\s+\S+\s*)?(?<ver>\s+.*?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase); 
        }

        public Type ChatType { get; }
        public Regex FirstMessage { get; }
        public Task<IConversation> StartConversation(IMainBot mainBot, ITgUser tgUserInfo, string firstMessage)
        {
            return Task<IConversation>.Factory.StartNew(() =>
            {
                var conversation = new UserStatLongTimeConversation(mainBot, tgUserInfo, firstMessage);
                conversation.ProcessConversation();
                return conversation;
            });
        }

        public class UserStatLongTimeConversation : IConversation
        {
            private IMainBot MainBot { get; }
            public ITgUser TgUser { get; }
            public string FirstMessage { get; }
            
            public UserStatLongTimeConversation(IMainBot mainBot, ITgUser tgUserInfo, string firstMessage)
            {
                this.MainBot = mainBot;
                this.TgUser = tgUserInfo;
                this.FirstMessage = firstMessage;
            }

            public Task ProcessMessage(string message)
            {
                return Task.CompletedTask;
            }

            public bool IsActive { get; set; }

            private int? MessageId { get; set; }


            public void ProcessConversation()
            {
                Task.Run(async () =>
                {
                    this.IsActive = true;
                    var messageText = await this.FormatMessageText();
                    this.MessageId = await this.MainBot.DirectSendMessage(this.TgUser, messageText);

                    while (this.IsActive)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3));
                        messageText = await this.FormatMessageText();
                        var msgId = this.MessageId;
                        if (msgId != null)
                            await this.MainBot.EditMessageText(this.TgUser, msgId.Value, messageText);
                    }
                });
            }
            
            private int _tmpCnt;
            private async Task<string> FormatMessageText()
            {
                this._tmpCnt++;
                if (this._tmpCnt > 10)
                {
                    this.IsActive = false;
                    return await Task.FromResult($"Пока выводит 10 раз. {DateTime.Now.ToLocalTime()}\nДальше обновления не будет.");
                }

                return await Task.FromResult($"Message {this._tmpCnt} {DateTime.Now.ToLocalTime()}");
            }
        }
    }
}