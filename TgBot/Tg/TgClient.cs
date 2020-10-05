using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBot.Bot;
using TgBot.Logger;

namespace TgBot.Tg
{
    public interface ITgClientService
    {
        /// <summary> Выслать сообщение </summary>
        Task<int> SendMessage(long tgChatId, [NotNull] string message);

        /// <summary> Обновить соощение </summary>
        Task EditMessage(long tgChatId, int messageId, string message);

        /// <summary> Проверить новые сообщения </summary>
        Task Pull();
    }

    /// <summary>
    /// Клиент телеграмма.
    /// Назначение:
    /// Отправляет-получает информацию в/из телеги. Следит за таймаутами и т.д. всё что связано с телегой.
    /// </summary>
    public class TgClientService : ITgClientService
    {
        private IMainBot MainBot { get; }
        
        private ILog Logger { get; }

        private TelegramBotClient TelegramClient { get; }

        private int TgMessageOffset { get; set; }

        public TgClientService([NotNull] TelegramBotClient tgClient, [NotNull] IMainBot mainBot, [NotNull] ILog logger)
        {
            this.MainBot = mainBot;
            this.Logger = logger;
            this.TelegramClient = tgClient;
            this.TelegramClient.OnMessage += OnMessage;
            this.TelegramClient.OnUpdate += OnUpdate;
            this.TelegramClient.OnReceiveError += OnReceiveError;
        }

        private void OnReceiveError(object? sender, ReceiveErrorEventArgs e)
        {
        }

        private void OnUpdate(object? sender, UpdateEventArgs e)
        {
        }

        private void OnMessage(object? sender, MessageEventArgs e)
        {
        }

        public async Task<int> SendMessage(long tgChatId, string message)
        {
            var chatId = new ChatId(tgChatId);
            var msg = await this.TelegramClient.SendTextMessageAsync(chatId, message, ParseMode.Html);
            return msg.MessageId;
        }

        public async Task EditMessage(long tgChatId, int messageId, string message)
        {
            
            var aa = await this.TelegramClient.EditMessageTextAsync(tgChatId, messageId, message, ParseMode.Html);
        }

        public async Task Pull()
        {
            var messages = await this.TelegramClient.GetUpdatesAsync(offset: this.TgMessageOffset);

            if (messages.Length != 0)
            {
                foreach (var msg in messages)
                {
                    if (msg.Message == null)
                        continue;
                    await this.MainBot.ProcessTeleramMessage(msg.Id, msg.Message.Chat.Id, msg.Message.Text ?? msg.Message.Caption ?? "");
                    _ = this.Logger.Info(msg.Message.Caption + "->" + msg.Message.Text);
                }

                this.TgMessageOffset = messages.Max(x => x.Id) + 1;
            }
            //var chatId = new ChatId(181969087);
            //var bb = this.TelegramClient.SendTextMessageAsync(chatId, "Привет.", ParseMode.Default).Result;
        }
    }
}