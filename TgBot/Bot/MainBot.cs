using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TgBot.Bot.ConversationVersion;
using TgBot.Logger;
using TgBot.Project;
using TgBot.Services;
using TgBot.Tg;

namespace TgBot.Bot
{
    /// <summary>
    /// Основной "бот".
    /// Назначение: Увязывает взаимодействие всех компонент.
    /// </summary>
    public interface IMainBot
    {
        /// <summary> Основной цикл работы бота. </summary>
        Task StartLoop();

        /// <summary> Процессинг сообщений. Разбирает что за сообщение, интересно ли пользователю и т.д. </summary>
        /// <param name="botMessage">Внутреннее сообщение</param>
        /// <param name="isQueue">Надо ли сообщение ставить в очередь. Если false и, например, не актуально по времени - сообщение игнорится</param>
        Task ProcessInternalMessage([NotNull] IBotMessage botMessage, bool isQueue);

        /// <summary> Список проектов </summary>
        IEnumerable<IProject> Projects { get; }

        /// <summary> Ссылка на redmine </summary>
        [NotNull]
        IRedmineService RedmineService { get; }

        /// <summary> Добавить "проект" в бот </summary>
        void AddProject([NotNull] IProject project);

        /// <summary> Добавить "пользователя" в бот </summary>
        void AddUser([NotNull] User user);

        /// <summary> Список пользователей </summary>
        /// <returns></returns>
        IEnumerable<User> Users();

        /// <summary> Добавить "чатик" в бот </summary>
        void AddGroup(Group group);
        
        /// <summary> Обработать входящее сообщение от пользователя </summary>
        /// <param name="tgMsgId">ИД сообщения телеграма</param>
        /// <param name="tgChatId">ИД чата телеграма</param>
        /// <param name="messageText">Собственно сообщение</param>
        Task ProcessTeleramMessage(int tgMsgId, long tgChatId, string messageText);

        /// <summary> Завершить диалог для пользователя </summary>
        void FinishDialog([NotNull] ITgUser tgUser);

        Task DirectSendMessage([NotNull] ITgUser tgUser, [NotNull] string message);
    }

    /// <summary>
    /// Основной "бот".
    /// Назначение: Увязывает взаимодействие всех компонент.
    /// </summary>
    public class MainBot : IMainBot
    {
        private Lazy<IRedmineService> _redmineService;

        /// <summary> Работа с телегой </summary>
        [NotNull]
        private Lazy<ITgClientService> TgService { get; }

        /// <summary> Сервис redmine </summary>
        [NotNull]
        public IRedmineService RedmineService => this._redmineService.Value;

        /// <summary> Логгер </summary>
        [NotNull]
        private ILog Logger { get; }

        public MainBot([NotNull] Lazy<ITgClientService> tgService, [NotNull] Lazy<IRedmineService> redmineService, [NotNull] ILog logger)
        {
            this.TgService = tgService;
            this._redmineService = redmineService;
            this.Logger = logger;
            this.AllProjects = new List<IProject>();
            this.AllUsers = new List<User>();
            this.AllGroups = new List<Group>();
            this.IsStarting = true;
        }

        private bool IsStarting { get; set; }

        public async Task StartLoop()
        {
            await this.Logger.Info("Начало");

            var background = new List<Task>();
            this.IsStarting = true;

            var cancelTokenSource = new CancellationTokenSource();
            var cancelToken = cancelTokenSource.Token;

            foreach (var project in this.AllProjects)
            {
                background.Add(project.UpdateVersion(cancelToken));
            }
            
            var alTaskStart = new List<Task>();
            alTaskStart.Add(this.RedmineService.UpdateTasks());
            foreach (var project in this.AllProjects)
            {
                alTaskStart.Add(project.PullNews());
            }

            Task.WaitAll(alTaskStart.ToArray());

            this.IsStarting = false;

            await this.SendMessageToAll("I live AGAIN!!!");

            while (true)
            {
                await Task.Delay(5000);

                // Обновляем таски отдельно, что б потом в джобах норм было
                await this.RedmineService.UpdateTasks();

                var allTasks = new List<Task>();
                //Пулинг изменений для пользователя
                foreach (var project in this.AllProjects)
                {
                    allTasks.Add(project.PullNews()); 
                }

                // Пулинг сообщений от пользователя
                allTasks.Add(this.TgService.Value.Pull());
                
                Task.WaitAll(allTasks.ToArray());
            }
        }

        private Task SendMessageToAll([NotNull] string message)
        {
            return this.ProcessInternalMessage(new BotMessageSimple(message), false);
        }

        public Task ProcessInternalMessage(IBotMessage botMessage, bool isQueue)
        {
            if (this.IsStarting)
                return Task.CompletedTask;

            this.Logger.Info("'{Message}' патипу {Type}", botMessage.Message, botMessage.GetType());


            var currentTime = DateTime.Now.TimeOfDay;
            var msgTasks = new List<Task>();

            foreach (var user in this.AllUsers.Where(x => x.TgId != null))
            {
                var isNeedSend = false;
                var messageText = botMessage.Message;

                if (botMessage is BotMessageRedmine redmineMessage)
                {
                    (isNeedSend, messageText) = this.ProcessInternalMessageRedmine(redmineMessage, user, isNeedSend, messageText);
                }
                else if (botMessage is BotMessageJob jobMessage)
                {
                    (isNeedSend, messageText) = this.ProcessInternalMessageJenkins(jobMessage, user, isNeedSend, messageText);
                }

                if (!isNeedSend)
                    continue;


                var maySend = false;
                if (user.StartTime < user.EndTime)
                    maySend = user.StartTime < currentTime && user.EndTime > currentTime;
                else
                    maySend = user.StartTime > currentTime || user.EndTime < currentTime;

                if (user.TemporaryDisable)
                    maySend = false;

                if (maySend)
                {
                    // Рассылаем сообщения
                    this.Logger.Info(botMessage.Message);
                    msgTasks.Add(this.TgService.Value.SendMessage(user.TgId.Value, messageText));

                }
                else if (isQueue)
                {
                    // Запихиваем сообщения в очередь
                }
            }

            return Task.WhenAll(msgTasks);
        }

        private (bool isNeedSend, string messageText) ProcessInternalMessageJenkins([NotNull] BotMessageJob jobMessage, [NotNull] User user, bool isNeedSend, [NotNull] string messageText)
        {
            foreach (var settingJob in user
                .ProjectsSettings
                .SelectMany(x => x.JenkinsVerbosity
                                                .Where(xx => xx.JobName == jobMessage.JobName)
                                                .Select(xx => new {Job = xx, Project = x}))
            )
            {
                if (settingJob.Project.IsActive)
                {
                    if (settingJob.Job.JenkinsVerbosity == EnumJenkinsVerbosity.Spam) 
                    {
                        isNeedSend = true;
                        break;
                    }
                    if (settingJob.Job.JenkinsVerbosity == EnumJenkinsVerbosity.FailOnly &&
                        jobMessage.JobStatus == EnumJobStatus.Fail)
                    {
                        isNeedSend = true;
                        break;
                    }
                    if (settingJob.Job.JenkinsVerbosity == EnumJenkinsVerbosity.SuccessOnly &&
                        jobMessage.JobStatus == EnumJobStatus.Success)
                    {
                        isNeedSend = true;
                        break;
                    }
                    if (settingJob.Job.JenkinsVerbosity == EnumJenkinsVerbosity.Final &&
                        (jobMessage.JobStatus == EnumJobStatus.Success || jobMessage.JobStatus == EnumJobStatus.Fail || jobMessage.JobStatus == EnumJobStatus.Aborted))
                    {
                        isNeedSend = true;
                        break;
                    }
                }
            }
            
            return (isNeedSend, messageText);
        }

        /// <summary> Обработка сообщений от редмайна </summary>
        private (bool isNeedSend, string messageText) ProcessInternalMessageRedmine([NotNull] BotMessageRedmine redmineMessage, [NotNull] User user, bool isNeedSend, [NotNull] string messageText)
        {
            foreach (var prj in this.AllProjects.Where(x => x.RedmineProjectName == redmineMessage.RdmProject))
            {
                var setting = user.ProjectsSettings.SingleOrDefault(x => x.ProjectName == prj.Name);
                if (setting != null)
                {
                    messageText += "\nПроект: <b>" + prj.Name + "</b>";

                    if (setting.IsActive)
                    {
                        if (setting.RedmineVerbosity == EnumRedmineVerbosity.Spam)
                        {
                            isNeedSend = true;
                            break;
                        }
                        if ((setting.RedmineVerbosity == EnumRedmineVerbosity.MyChanged
                                    || setting.RedmineVerbosity == EnumRedmineVerbosity.AssigningToMe)
                            && redmineMessage.RdmAssignTo == user.RedmineUser
                            && redmineMessage.RdmPrevAssignTo != user.RedmineUser)
                        {
                            messageText = "<b>Вам назначена задача</b>\n\n\n" + messageText;
                            isNeedSend = true;
                            break;
                        }
                        if (setting.RedmineVerbosity == EnumRedmineVerbosity.MyChanged && redmineMessage.RdmAssignTo == user.RedmineUser)
                        {
                            isNeedSend = true;
                            break;
                        }
                        if (setting.RedmineVerbosity == EnumRedmineVerbosity.VersionChange)
                        {
                            // TODO пока не сделал...
                        }
                    }
                }
            }

            return (isNeedSend, messageText);
        }

        /// <summary> Проекты </summary>
        public IEnumerable<IProject> Projects { get { return this.AllProjects; } }

        /// <summary> Все проекты </summary>
        [NotNull]
        private List<IProject> AllProjects { get; }

        public void AddProject(IProject project)
        {
            this.AllProjects.Add(project);
        }

        /// <summary> Все пользователи </summary>
        [NotNull]
        private List<User> AllUsers { get; }

        public void AddUser([NotNull] User user)
        {
            this.AllUsers.Add(user);
        }

        public IEnumerable<User> Users()
        {
            return this.AllUsers;
        }

        /// <summary> Все группы/чатики </summary>
        [NotNull]
        private List<Group> AllGroups { get; }

        public void AddGroup([NotNull] Group group)
        {
            this.AllGroups.Add(group);
        }


        /// <summary> Типы диалогов </summary>
        private List<IConversationFactory> ConversationFactories { get; } = new List<IConversationFactory>
        {
            new GroupVersionConversationFactory(),
            new UserVersionConversationFactory()

        };

        /// <summary> Открытые диалоги </summary>
        private ConcurrentDictionary<ITgUser, IConversation> Conversations { get; } = new ConcurrentDictionary<ITgUser, IConversation>();

        public Task ProcessTeleramMessage(int tgMsgId, long tgChatId, string messageText)
        {
            var tgInfo = (ITgUser)this.AllUsers.SingleOrDefault(x => x.TgId == tgChatId) ?? this.AllGroups.SingleOrDefault(x => x.TgId == tgChatId);
            if (tgInfo == null)
            {
                this.Logger.Info("Неизвестный чат {ChatId} сообщение '{Message}'", tgChatId, messageText);
                return Task.CompletedTask;
            }

            this.Logger.Info("{Type} {UserName} чат {ChatId} сообщение '{Message}'", tgInfo.GetType().Name, tgInfo.Name, tgChatId, messageText);

            IConversation openConversation;
            if (this.Conversations.TryGetValue(tgInfo, out openConversation))
            {
                return openConversation.ProcessMessage(messageText);
            }

            var chatType = tgInfo.GetType();
            var conversationFactory = this.ConversationFactories.SingleOrDefault(x => x.ChatType == chatType && x.FirstMessage.IsMatch(messageText));
            if (conversationFactory != null)
            {
                return Task.Factory.StartNew(() =>
                {
                    var conversation = conversationFactory.StartConversation(this, tgInfo, messageText).Result;
                    if (conversation.IsActive)
                        this.Conversations.TryAdd(tgInfo, conversation);
                });
            }

            return Task.CompletedTask;
        }

        public void FinishDialog(ITgUser tgUser)
        {
            this.Conversations.TryRemove(tgUser, out _);
        }

        public async Task DirectSendMessage(ITgUser tgUser, string message)
        {
            await this.TgService.Value.SendMessage(tgUser.TgId.Value, message);
        }
    }
}