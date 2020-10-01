using JetBrains.Annotations;
using TgBot.Services;

namespace TgBot.Bot
{
    public enum EnumBotMessageType
    {
        /// <summary> Выполнение job'ы build сервера </summary>
        BuildJobExecution,
        
        /// <summary> Отловленные изменения по таску в redmine </summary>
        /// <remarks>
        /// Тут надо бы какую-то ещё обработку делать.
        ///
        /// Возможны варианты:
        /// - таск "как-то" поменялся
        /// - новый таск
        /// - принадлежность таска поменялась (заасайнили таск тебе)
        /// - Поменялся статус таска (отрезолвился)
        /// </remarks>
        TaskInfo,
        
        /// <summary> Простое сообщение пользователю </summary>
        Simple
    }


    public interface IBotMessage
    {
        /// <summary> Тип сообщения </summary>
        public EnumBotMessageType JobMessageType { get; }

        /// <summary> Основной заголовок </summary>
        [NotNull]
        public string Message { get; }
    }

    public class BotMessageRedmine : IBotMessage
    {
        /// <summary> Тип сообщения </summary>
        public EnumBotMessageType JobMessageType { get; }

        /// <summary> Основной заголовок </summary>
        public string Message { get; }

        /// <summary> Проект redmine</summary>
        public string RdmProject { get; set; }

        /// <summary> Кому назначено по данны redmine </summary>
        public string RdmAssignTo { get; set; }

        /// <summary> Кому _была_ назначено по данны redmine </summary>
        public string RdmPrevAssignTo { get; set; }

        /// <summary> Внутреннее сообщение бота </summary>
        /// <param name="mainMessage">Основное сообщение</param>
        public BotMessageRedmine([NotNull] string mainMessage)
        {
            this.JobMessageType = EnumBotMessageType.TaskInfo;
            this.Message = mainMessage;
        }
    }

    /// <summary> "Сообщение" бота. То что вылетает из всех puller'ов </summary>
    public class BotMessageJob : IBotMessage
    {
        /// <summary> Тип сообщения </summary>
        public EnumBotMessageType JobMessageType { get; }
        
        /// <summary> Основной заголовок </summary>
        public string Message { get; }
        
        /// <summary> Имя джобы в jenkins </summary>
        public string JobName { get;  }

        /// <summary> Статус джобы </summary>
        public EnumJobStatus JobStatus { get; }

        /// <summary>
        /// Дополнительная информация по сообщению (собственно, само сообщение)
        /// </summary>
        [NotNull] 
        public string InfoMessage { get; }

        /// <summary> Внутреннее сообщение бота </summary>
        /// <param name="jobName">Имя джобы</param>
        /// <param name="jobStatus">Статус джобы</param>
        /// <param name="mainCaption">Основной заголовок</param>
        /// <param name="infoMessage">Дополнительное сообщение</param>
        public BotMessageJob([NotNull] string jobName, EnumJobStatus jobStatus, [NotNull] string mainCaption, [NotNull] string infoMessage)
        {
            this.JobMessageType = EnumBotMessageType.BuildJobExecution;
            this.JobName = jobName;
            this.JobStatus = jobStatus;
            this.Message = mainCaption;
            this.InfoMessage = infoMessage;
        }

    }

    /// <summary> Простое сообщение пользователю </summary>
    public class BotMessageSimple : IBotMessage
    {
        public EnumBotMessageType JobMessageType { get; }
        public string Message { get; }

        public BotMessageSimple([NotNull] string message)
        {
            this.JobMessageType = EnumBotMessageType.Simple;
            this.Message = message;
        }
    }
}