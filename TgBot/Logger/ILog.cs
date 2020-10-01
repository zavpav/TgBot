using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TgBot.Logger
{
    public interface ILog
    {
        void InfoSync([CanBeNull] string messageTemplate, [CanBeNull] params object[] args);

        Task Info([CanBeNull] string messageTemplate, [CanBeNull] params object[] args);
    }
}