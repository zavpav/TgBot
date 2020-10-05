using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using TgBot.AppConfiguration;
using TgBot.Bot;
using TgBot.Logger;
using TgBot.Services;
using TgBot.Tg;

namespace TgBot.Project
{
    /// <summary> Информация по "проекту" </summary>
    public class JenkinsProject : IProject
    {
        public JenkinsProject([NotNull] string name,
            [NotNull] ILog logger,
            [NotNull] AppConfiguration.AppConfiguration configuration,
            [NotNull] IMainBot mainBot, 
            [NotNull] IJenkinsService jenkinsService,
            [NotNull] IRedmineService redmineService)
        {
            this.Name = name;
            this.Logger = logger;
            this.Configuration = configuration;
            this.MainBot = mainBot;
            this.JenkinsService = jenkinsService;
            this.RedmineService = redmineService;
            this.BuildServerPulles = new List<JenkinsJobInfo>();
            this.VersionCurrent = "0";
        }

        /// <summary> "Пользовательское" наименование проекта </summary>
        public string Name { get; }

        /// <summary> Логгер </summary>
        [NotNull] 
        private ILog Logger { get; }

        /// <summary> Основные классы конфигурации </summary>
        [NotNull] 
        private AppConfiguration.AppConfiguration Configuration { get; }

        /// <summary> Ссылка на основного бота </summary>
        public IMainBot MainBot { get; }

        /// <summary> Ссылка на работу с jenkins </summary>
        [NotNull]
        private IJenkinsService JenkinsService { get; }

        /// <summary> Наименование проекта в redmine </summary>
        public string RedmineProjectName { get; set; }

        /// <summary>
        /// Статусы тасков, привязанные к разным ролям
        /// </summary>
        private Dictionary<EnumUserRole, List<string>> RedmineStatuses = new Dictionary<EnumUserRole, List<string>>
        {
            {EnumUserRole.Developer, new List<string>{"Готов к работе"}}
        };

        /// <summary> Взаимодействие с Redmine </summary>
        [NotNull]
        private IRedmineService RedmineService { get; }

        /// <summary> Список джобов </summary>
        public IEnumerable<JenkinsJobInfo> Jobs => this.BuildServerPulles;

        public string VersionCurrent { get; private set; }

        public string LastBuildCommitHash { get; set; } = "6b7dad96ad0c872b588f4b44cdca97cae0c69a92";


        private DateTime _lastUpdateVersion;

        /// <summary> Пинание сервисов работы с серверами (jenkins, git, redmine) </summary>
        public async Task PullNews()
        {
            try
            {
                if (!string.IsNullOrEmpty(this.JenkinsPrefix))
                {
                    // Версию обновляем раз в 15 минут
                    if (DateTime.Now - this._lastUpdateVersion > new TimeSpan(0, 0, 15, 0))
                    {
//                        this.UpdateVersion();
                        //var vers = await this.JenkinsService.GetVersion(this.JenkinsPrefix);
                        //this.VersionCurrent = vers["Current"];
                        //this._lastUpdateVersion = DateTime.Now;
                    }
                }

                foreach (var jobInfo in this.BuildServerPulles)
                {
                    var newExecutions = await this.JenkinsService.GetExecutionsList(jobInfo.JobName, jobInfo.LastUpdate);
                    foreach (var executionInfo in newExecutions)
                    {
                        var reformatMessage = $"<b>Сборка {executionInfo.JobNum} Проект: {this.Name} ({jobInfo.JobDesc}) {executionInfo.Status}</b>\n\n";

                        if (jobInfo.JobType == EnumJobType.BuildApp)
                        {
                            try
                            {
                                reformatMessage += this.FormatMessageInfoBuildApp(executionInfo);
                            }
                            catch
                            {

                            }
                        }
                        else if (jobInfo.JobType == EnumJobType.Dump)
                        {
                            try
                            {
                                reformatMessage += this.FormatMessageInfoDump(executionInfo);
                            }
                            catch
                            {

                            }
                        }
                        else
                        {
                            try
                            {
                                reformatMessage += this.FormatMessageInfoSystem(executionInfo);
                            }
                            catch
                            {

                            }
                        }

                        var botMessage = new BotMessageJob(jobInfo.JobName, executionInfo.Status, reformatMessage, "");
                        await this.MainBot.ProcessInternalMessage(botMessage, true);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task UpdateVersion(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();
                try
                {
                    var fileData = await File.ReadAllTextAsync("appsettings.projectversions.json", cancellationToken);
                    var fileVersions = JsonConvert.DeserializeObject<List<ProjectVersion>>(fileData);

                    var version = fileVersions.SingleOrDefault(x => x.Name == this.Name);
                    if (version == null)
                    {
                        await this.Logger.Info($"Ошибка определения версии ${this.Name}").ConfigureAwait(false);
                    }
                    else
                    {
                        this.VersionCurrent = version.Version;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }

        public void SetRedmineInfo([NotNull] string redmineProjectName)
        {
            this.RedmineProjectName = redmineProjectName;
        }



        /// <summary> Отформатировать сообщение сборки версии </summary>
        /// <remarks>
        /// Тут не важно описание джобы (тем более оно в 99% отсутствует).
        /// Зато ChageSet форматируется следующим образом:
        /// 1. Если InProgress: Форматируется как для системы (изменения по git).
        /// 2. Если Faild: Формируется по задачам redmine + те что без задачи из гита.
        /// 3. Если Success: Формируется по всем задачам из redmine начиная с последней успешной.
        /// </remarks>
        private string FormatMessageInfoBuildApp([NotNull] BuildServerExecutionInfo executionInfo)
        {
            var msg = "";

            var gitMessages = executionInfo.ChangeSet;

            if (executionInfo.Status == EnumJobStatus.Success)
            {
                var prevFailed = this.JenkinsService.GetAllPrevSuccessInfos(executionInfo);
                foreach (var ch in prevFailed.SelectMany(x => x.ChangeSet))
                    gitMessages.Add(ch);
            }

            if (executionInfo.Status == EnumJobStatus.Success)
            {
                var baseClientDirectory = this.Configuration.Projects?
                        .SingleOrDefault(x => x.ProjectName == this.Name)?
                        .ProjectClientDirectory;
                
                if (!string.IsNullOrEmpty(baseClientDirectory))
                {
                    msg += $"Клиент: \"{baseClientDirectory}{this.VersionCurrent}.00.{executionInfo.JobNum}\"";
                }
            }

            var redmineNums = new HashSet<string>();
            var otherMessages = new List<string>();

            var reFindRedmineNums = new Regex(@"#(\d+)(\s|\n|$)", RegexOptions.Compiled);
            foreach (var gitMessage in gitMessages)
            {
                var mch = reFindRedmineNums.Match(gitMessage);
                if (mch.Success)
                {
                    redmineNums.Add(mch.Groups[1].Value);
                }
                else
                {
                    otherMessages.Add(gitMessage.EscapeHtml().Trim());
                }
            }

            if (redmineNums.Count != 0)
            {
                msg += "<b>Изменения по следующим задачам:</b>\n";

                var allRedmineTasks = redmineNums.Select(x => this.RedmineService.TryGetTaskDescription(x)).ToList();

                List<RedmineTaskDesc> projectTasks;
                var externalTasks = new List<RedmineTaskDesc>();
                if (this.RedmineProjectName != null)
                {
                    projectTasks = allRedmineTasks.Where(x => x.Project == this.RedmineProjectName).ToList();
                    externalTasks = allRedmineTasks.Where(x => x.Project != this.RedmineProjectName).ToList();
                }
                else
                {
                    projectTasks = allRedmineTasks;
                }

                foreach (var taskDesc in projectTasks)
                    msg += $"<a href=\"{this.RedmineService.FormatTaskAddress(taskDesc.Num)}\">#{taskDesc.Num}</a> {taskDesc.Subject}\n";

                if (externalTasks.Any())
                {
                    msg += "\n\n\n<b>Изменения по другим проектам:</b>\n";

                    foreach (var prj in externalTasks.GroupBy(x => x.Project))
                    {
                        foreach (var taskDesc in prj.OrderBy(x => x.Num))
                            msg += $"{prj.Key} <a href=\"{this.RedmineService.FormatTaskAddress(taskDesc.Num)}\">#{taskDesc.Num}</a> {taskDesc.Subject}\n";
                    }



                }
            }
                
            if (otherMessages.Count != 0)
                msg += "\n\n\nДругие комментарии:\n" + string.Join("\n", otherMessages);

            return msg;
        }

        /// <summary> Отформатировать сообщение сборки дампа </summary>
        /// <remarks>
        /// Тут изменения не важны. Но важно будет описание джобы, что б вытащить информацию по дампам и базам
        /// </remarks>
        private string FormatMessageInfoDump([NotNull] BuildServerExecutionInfo executionInfo)
        {
            var msg = "";

            if (!string.IsNullOrEmpty(executionInfo.JobDescription))
            {
                //SdFu Db: 208.0.0.135/sdfu
                //Fl: SdFu.2020.02.09.23.36.04

                //Upd: C:\Builds\SdFu\91.rc.19
                //dbV: 90 rcV: 91 currV: 92 isRc: false
                //Inst: C:\Builds\SdFu\90.rc.16

                var dbNameMatch = Regex.Match(executionInfo.JobDescription, "Db: (.*?)\\n").Groups[1];
                var fileNameMatch = Regex.Match(executionInfo.JobDescription, "Fl: (.*?)\\n").Groups[1];
                
                if (dbNameMatch.Success && fileNameMatch.Success)
                {
                    msg += "База: <b>" + dbNameMatch.Captures[0] + "</b>\n";

                    var dmpDateString = fileNameMatch.Captures[0].Value;
                    var dateParseMatch = Regex.Match(dmpDateString, @"[^\.]+\.(?<year>\d+)\.(?<mm>\d+)\.(?<dd>\d+)\.(?<tm>.*)");
                    if (dateParseMatch.Success)
                    {
                        dmpDateString = dateParseMatch.Groups["dd"].Value + "." + dateParseMatch.Groups["mm"].Value +
                                        "." + dateParseMatch.Groups["year"].Value + " (" +
                                        dateParseMatch.Groups["tm"].Value + ")";
                    }

                    msg += "Дамп от: <b>" + dmpDateString + "</b>\n";
                }

                msg += "\n\nПрочая информация:\n" + executionInfo.JobDescription;
            }


            return msg;
        }

        /// <summary> Отформатировать системное сообщение </summary>
        [NotNull]
        private string FormatMessageInfoSystem([NotNull] BuildServerExecutionInfo executionInfo)
        {
            var msg = "";
            if (!string.IsNullOrEmpty(executionInfo.JobDescription))
                msg += executionInfo.JobDescription + "\n\n";

            if (executionInfo.ChangeSet.Any())
            {
                var reRedmineParse = new Regex(@"(#(\d+))(\s|$)");

                msg += "<i>Изменения:</i>" + "\n";

                foreach (var chengeComment in executionInfo.ChangeSet.Distinct())
                {
                    var taskAddress = this.RedmineService.FormatTaskAddress("");
                    var chStr = reRedmineParse.Replace(chengeComment.EscapeHtml().Trim(),
                        @"<a href=""" + taskAddress + @"$2"">$1</a>$3");
                    msg += chStr + "\n";
                }
            }

            return msg;
        }

      
        /// <summary> Промежуточный классик (пока не придумал как именно хоронить это) </summary>
        public class JenkinsJobInfo
        {
            /// <summary> Описание джобы (для пользователя) </summary>
            public string JobDesc { get; set; }
            
            /// <summary> Имя джобы на сервере </summary>
            public string JobName { get; set; }

            /// <summary> Номер последнего проверенного билда </summary>
            public int? LastJobInfoUpdate { get; set; }

            public DateTime? LastUpdate { get; set; }
            
            /// <summary> Тип сборки </summary>
            public EnumJobType JobType { get; set; }
        }

        public enum EnumJobType
        {
            /// <summary> Системные </summary>
            System,

            /// <summary> Поднятие дампов </summary>
            Dump,

            /// <summary> Сборка приложений </summary>
            BuildApp
        }

        /// <summary> Списко джобов jenkins привязанных к этому проекту </summary>
        [NotNull]
        private List<JenkinsJobInfo> BuildServerPulles { get; }

        /// <summary> Добавить пулер билд сервера </summary>
        /// <param name="jobDesc">Имя задачи "для пользователя"</param>
        /// <param name="jobName">Имя задачи на сервере</param>
        /// <param name="jobType">Тип сборки</param>
        public void AddBuildPuller([NotNull] string jobDesc, [NotNull] string jobName, EnumJobType jobType)
        {
            this.BuildServerPulles.Add(new JenkinsJobInfo { JobDesc = jobDesc, JobName = jobName, JobType = jobType });
        }


        /// <summary> Установка информации для получения версии из jenkins </summary>
        [CanBeNull]
        public string JenkinsPrefix { get; set; }

        /// <summary> Имя сервиса гита </summary>
        [CanBeNull]
        public string GitName { get; set; }
    }
}