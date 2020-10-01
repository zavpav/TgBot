using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TgBot;

namespace TgBot
{

    public enum EnumUserRole
    {
        Total,
        Developer,
        Tester,
        Boss,
        Analist
    }

    public interface ITgUser
    {
        /// <summary> ИД пользователя телеграма </summary>
        public long? TgId { get; set; }

        /// <summary> Имя пользователя </summary>
        public string Name { get; set; }
     
        EnumUserRole Role { get; set; }
    }

    public class Group : ITgUser
    {
        public long? TgId { get; set; }
        public string Name { get; set; }
        public EnumUserRole Role { get; set; }
    }

    /// <summary> "Пользователь" телеги </summary>
    public class User : ITgUser
    {
        public User()
        {
            this.ProjectsSettings = new List<ProjectUserSetting>();
        }

        /// <summary> ИД пользователя телеграма </summary>
        public long? TgId { get; set; }
        
        /// <summary> Имя пользователя </summary>
        public string Name { get; set; }

        /// <summary> Имя (а может ИД) пользователя для поиска задач в redmine </summary>
        public string RedmineUser { get; set; }

        /// <summary> Время начала работы пользователя </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary> Время окончание работы пользователя </summary>
        public TimeSpan EndTime { get; set; }
        
        /// <summary> Временная блокировка пользователя </summary>
        public bool TemporaryDisable { get; set; }

        /// <summary> Настройки по проектам </summary>
        [NotNull, ItemNotNull]
        public List<ProjectUserSetting> ProjectsSettings { get; }

        public EnumUserRole Role { get; set; }
    }

    public class ProjectUserSetting
    {
        public ProjectUserSetting()
        {
            this.JenkinsVerbosity = new List<JenkinsJobUserSettings>();
        }

        /// <summary> Активен ли проект </summary>
        public bool IsActive { get; set; }

        /// <summary> Имя проекта </summary>
        public string ProjectName { get; set; }

        /// <summary> Болтливость по проекту из redmine </summary>
        public EnumRedmineVerbosity RedmineVerbosity { get; set; }

        /// <summary> Болтливость джобов jenkins </summary>
        public List<JenkinsJobUserSettings> JenkinsVerbosity { get; }
    }

    /// <summary> Спамливость изменений редймайна </summary>
    public enum EnumRedmineVerbosity
    {
        /// <summary> Не спамить </summary>
        None,

        /// <summary> Новые задачи назначенные мне. </summary>
        AssigningToMe,

        //Возможно ещё добавить: изменено моё актуальное - изменения назначенное мне и попадающее в версию

        /// <summary> Что-то из назначенного мне поменялось. </summary>
        MyChanged,

        /// <summary> Что-то поменялось в актуальных версиях </summary>
        VersionChange,

        /// <summary> Летит всё-всё по проекту </summary>
        Spam
    }
    
    public class JenkinsJobUserSettings
    {
        public string JobName { get; set; }

        public EnumJenkinsVerbosity JenkinsVerbosity { get; set; }
    }

    /// <summary> Болтливость по дженкинсу </summary>
    public enum EnumJenkinsVerbosity
    {
        /// <summary> Не спамить </summary>
        None,
        
        /// <summary> Только фейлы </summary>
        FailOnly,

        /// <summary> Только успешные сборки </summary>
        SuccessOnly,

        /// <summary> "Финальные" статусы (фейл, успех, отмена) </summary>
        Final,

        /// <summary> Любые изменения джобы </summary>
        Spam,
    }
}