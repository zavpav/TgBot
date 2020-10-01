using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TgBot.Bot
{
    /// <summary> Диалог </summary>
    public interface IConversation
    {
        /// <summary> Пользователь, с которым диалог </summary>
        [NotNull]
        ITgUser TgUser { get; }

        /// <summary> Обработать сообщение </summary>
        Task ProcessMessage([NotNull] string message);

        /// <summary> Диалог ещё активен </summary>
        bool IsActive { get; }
    }
}