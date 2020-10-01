namespace TgBot.Tg
{
    /// <summary>
    /// Сообщение в телегу
    /// </summary>
    public class TgMessage
    {
        public string Message { get; set; }
        public long UserId { get; set; }
    }
}