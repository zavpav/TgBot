using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TgBot.Bot
{
    public interface IConversationFactory
    {
        /// <summary> Тип чата (личные сообщения или чат) </summary>
        [NotNull]
        Type ChatType { get; }

        /// <summary> Первое сообщение для начала диалога </summary>
        [NotNull]
        Regex FirstMessage { get; }

        /// <summary> Начать диалог </summary>
        /// <param name="mainBot">Основной бот</param>
        /// <param name="tgUserInfo">Информация о "пользователе"</param>
        /// <param name="firstMessage">Первое сообщение</param>
        [CanBeNull]
        Task<IConversation> StartConversation([NotNull] IMainBot mainBot, [NotNull] ITgUser tgUserInfo, [NotNull] string firstMessage);
    }
}