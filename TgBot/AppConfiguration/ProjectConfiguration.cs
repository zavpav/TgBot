using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using TgBot.Project;

namespace TgBot.AppConfiguration
{
    public class ProjectConfiguration
    {
        /// <summary> Наименование проекта </summary>
        [CanBeNull]
        public string ProjectName { get; set; }

        /// <summary> Базовая директория для клиента </summary>
        [CanBeNull]
        public string ProjectClientDirectory { get; set; }

        /// <summary> Конфигурация билдов jenkins </summary>
        [CanBeNull]
        public JenkinsJobConfiguration[] Builds { get; set; }

        public string JenkinsPrefix { get; set; }
        
        public string RedmineProject { get; set; }

        /// <summary> Описание получения данных из Гита </summary>
        [CanBeNull]
        public string GitName { get; set; }

        public class JenkinsJobConfiguration
        {
            [CanBeNull]
            public string Desc { get; set; }
            [CanBeNull]
            public string Job { get; set; }
            public JenkinsProject.EnumJobType JobType { get; set; }
        }

    }
}