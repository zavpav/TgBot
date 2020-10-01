using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TgBot.Bot
{

    /// <summary> Простой "диалог" отсылающий одно сообщение </summary>
    public class SimpleMessageConversation : IConversation
    {
        public SimpleMessageConversation([NotNull] IMainBot mainBot, [NotNull] ITgUser tgUserInfo)
        {
            this.MainBot = mainBot;
            this.TgUser = tgUserInfo;
            this.IsActive = true;
        }

        [NotNull]
        private IMainBot MainBot { get; }

        public ITgUser TgUser { get; }

        public Task ProcessMessage(string message)
        {
            this.MainBot.DirectSendMessage(this.TgUser, message);

            this.MainBot.FinishDialog(this.TgUser);
            this.IsActive = false;
            return Task.CompletedTask;
        }

        public bool IsActive { get; private set; }
    }
}