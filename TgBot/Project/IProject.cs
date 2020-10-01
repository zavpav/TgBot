using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TgBot.Bot;

namespace TgBot.Project
{
    /// <summary> "Проект" бота </summary>
    public interface IProject
    {
        /// <summary> Наименование </summary>
        [NotNull]
        string Name { get; }

        /// <summary> Проверить "новости" по проекту </summary>
        Task PullNews();

        [CanBeNull]
        string RedmineProjectName { get; }

        /// <summary> Список задач сборки </summary>
        [NotNull, ItemNotNull]
        IEnumerable<JenkinsProject.JenkinsJobInfo> Jobs { get; }
        
        /// <summary> Основной проект </summary>
        [NotNull]
        IMainBot MainBot { get; }

        /// <summary> Текущая версия </summary>
        [NotNull]
        string VersionCurrent { get; }

        /// <summary> Обновить версию проекта </summary>
        Task UpdateVersion(CancellationToken cancellationToken);
    }
}