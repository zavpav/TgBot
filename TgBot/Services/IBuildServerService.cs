using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TgBot.Services
{
    /// <summary> Взаимодействие с сервером билдов </summary>
    public interface IBuildServerService
    {
        /// <summary> Получить список исполнений нужного джоба </summary>
        /// <param name="jobName">Имя джобы</param>
        /// <param name="lastJobUpdate">Номер последего проверенного билда. Если null - действуем "по умалчанию"</param>
        /// <returns>Информация по текущему исполнению</returns>
        [NotNull]
        Task<List<BuildServerExecutionInfo>> GetExecutionsList([NotNull] string jobName, DateTime? lastJobUpdate);
    }
}