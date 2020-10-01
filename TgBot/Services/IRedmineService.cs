
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace TgBot.Services
{
    public interface IRedmineService
    {
        RedmineTaskDesc TryGetTaskDescription(string taskNum);
        
        /// <summary> Форматирование адреса для таска </summary>
        [NotNull]
        string FormatTaskAddress(string taskNum);

        /// <summary> Проверить обновления в редмайне </summary>
        [NotNull]
        Task UpdateTasks();

        /// <summary> Получить список задач по версии </summary>
        Task<List<RedmineTaskDesc>> RedmineTasks(string redmineProjectName, string version);
    }
}