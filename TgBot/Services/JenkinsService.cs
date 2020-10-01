using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using JetBrains.Annotations;
using TgBot.Logger;
using TgBot.Tg;

namespace TgBot.Services
{

    /// <summary> Взаимодействие с дженкинсом </summary>
    public interface IJenkinsService : IBuildServerService
    {
        /// <summary> Получить все предыдущие зафейленые выполнения джобы </summary>
        [NotNull] 
        List<BuildServerExecutionInfo> GetAllPrevSuccessInfos([NotNull] BuildServerExecutionInfo executionInfo);

        /// <summary> Получить информацию по версиям </summary>
        /// <param name="projectName">Имя проекта для поиска переменных</param>
        Task<Dictionary<string, string>> GetVersion([NotNull] string projectName);
    }

    public enum EnumJobStatus
    {
        // Не определено (например при выполнении, когда сатуса толком нет)
        Undef,
        InProgress,
        Success,
        Fail,
        Aborted
    }

    public class BuildServerExecutionInfo
    {
        /// <summary> Имя джобы в jenkins (по идее - дубль, но, думаю, так удобнее) </summary>
        public string JobName { get; set; } = "";

        /// <summary> Статус билда </summary>
        public EnumJobStatus Status { get; set; } = EnumJobStatus.Undef;

        /// <summary> Номер билда </summary>
        public int JobNum { get; set; } = -1;

        public DateTime LastRefreshJob { get; set; }

        /// <summary> Описание билда (актуально для дампов, для остальных - пустышка) </summary>
        public string JobDescription { get; set; } = "";

        /// <summary> Описание изменений </summary>
        public List<string> ChangeSet { get; set; } = new List<string>();

        public void CopyFrom([NotNull] BuildServerExecutionInfo executionInfo)
        {
            this.JobName = executionInfo.JobName;
            this.JobNum = executionInfo.JobNum;
            this.Status = executionInfo.Status;
            this.JobDescription = executionInfo.JobDescription;
            this.LastRefreshJob = executionInfo.LastRefreshJob;
            this.ChangeSet = executionInfo.ChangeSet;
        }
    }

    /// <summary> Взаимодействие с дженкинсом </summary>
    public class JenkinsService : IJenkinsService
    {
        /// <summary> Логгер </summary>
        [NotNull]
        private ILog Logger { get; }

        public JenkinsService([NotNull] ILog logger, [NotNull] AppConfiguration.AppConfiguration appConfiguration)
        {
            this.Logger = logger;
            this.AllExecInfos = new BlockingCollection<BuildServerExecutionInfo>();
            this._jenkinsAddress = appConfiguration.Services?.Jenkins?.Address ?? throw new NotSupportedException("Ошибка конфигурации. Не задан сервер Jenkins");


            var name = appConfiguration.Services?.Jenkins?.NetworkCredential?.Name;
            var pass = appConfiguration.Services?.Jenkins?.NetworkCredential?.Password;
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(pass)) { }
            else if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(pass))
            {
                this.JenkinsAuthFunc = request =>
                {
                    string basicAuthToken = Convert.ToBase64String(Encoding.Default.GetBytes("bot:1"));
                    request.Headers["Authorization"] = "Basic " + basicAuthToken;
                };
            }
            else
                throw new NotSupportedException("Ошибка конфигурации auth jenkins");

        }

        /// <summary> Функция определяющая аутентификацию </summary>
        [CanBeNull]
        private Action<WebRequest> JenkinsAuthFunc { get; set; }

        /// <summary> Адрес jenkins </summary>
        [NotNull]
        private readonly string _jenkinsAddress;

        // ReSharper disable once InconsistentNaming
        private readonly TimeSpan CheckTimeInterval = new TimeSpan(0, 0, 0, 10);

        /// <summary> Список всех "выполнений" </summary>
        /// <remarks>
        /// Concurrent нужен исключительно для безопасного enumeration
        /// </remarks>
        [NotNull]
        private BlockingCollection<BuildServerExecutionInfo> AllExecInfos { get; set; }

        public async Task<List<BuildServerExecutionInfo>> GetExecutionsList(string jobName, DateTime? lastJobUpdate)
        {
            var lastUpdateJob = this.AllExecInfos
                        .Where(x => x.JobName == jobName)
                        .OrderBy(x => x.LastRefreshJob)
                        .LastOrDefault();

            var lastUpdateTime = DateTime.Now;
            if (lastUpdateJob != null)
                lastUpdateTime = lastUpdateJob.LastRefreshJob;

            if (lastUpdateJob == null || DateTime.Now - lastUpdateJob.LastRefreshJob > this.CheckTimeInterval)
                await this.RefreshJobs(jobName);

            return this.AllExecInfos.Where(x =>
                x.JobName == jobName && (lastUpdateJob == null || x.LastRefreshJob > lastUpdateTime)).ToList();
        }

        public List<BuildServerExecutionInfo> GetAllPrevSuccessInfos(BuildServerExecutionInfo executionInfo)
        {
            return this.AllExecInfos
                .Where(x => x.JobName == executionInfo.JobName && x.JobNum < executionInfo.JobNum)
                .OrderByDescending(x => x.JobNum)
                .TakeWhile(x => x.Status != EnumJobStatus.Success)
                .ToList();
        }

        /// <summary> Получить данные дженкинса через API </summary>
        private async Task<List<BuildServerExecutionInfo>> GetJenkinsData([NotNull] string jobName)
        {
            //Лог http://208.0.0.1:8081/job/FbpfTestPromDump/249/logText/progressiveText?start=0 
            //Список билдов http://208.0.0.1:8081/job/FbpfTestPromDump/api/xml
            //Детально по билду http://208.0.0.1:8081/job/FbpfTestPromDump/249/api/xml

            //http://208.0.0.1:8081/job/AsudTestPromDump/api/xml?tree=allBuilds[number,result,timestamp,building]{0,10}
            var listRequest = this.CreateRequest("/job/" + jobName + "/api/xml?tree=allBuilds[number,result,timestamp,building]{0,10}");
            XDocument xListDocument = null;
            using (var response = (HttpWebResponse) (await listRequest.GetResponseAsync()))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                    xListDocument = await XDocument.LoadAsync(response.GetResponseStream(), LoadOptions.None,
                        CancellationToken.None);
            }


            if (xListDocument != null)
            {
                var jobList = this.AllExecInfos.Where(x => x.JobName == jobName).ToList();

                var needUpdate = new ConcurrentBag<Tuple<int, bool, string>>();
                foreach (var xElement in xListDocument.Element("workflowJob").Elements("allBuild"))
                {
                    // ReSharper disable PossibleNullReferenceException
                    var jobNum = int.Parse(xElement.Element("number").Value);
                    var isBuilding = bool.Parse(xElement.Element("building").Value);

                    var result = xElement.Element("result")?.Value ?? "";
                    // ReSharper restore PossibleNullReferenceException

                    // Пропускаем обработанное
                    if (jobList.Any(x =>
                        (x.Status == EnumJobStatus.Success || x.Status == EnumJobStatus.Fail ||
                         x.Status == EnumJobStatus.Aborted) && x.JobNum == jobNum))
                        continue;

                    needUpdate.Add(Tuple.Create(jobNum, isBuilding, result));
                }

                var resultBag = new ConcurrentBag<BuildServerExecutionInfo>();
                await Task.WhenAll(Partitioner
                    .Create(needUpdate)
                    .GetPartitions(5)
                    .Select(partition =>
                        Task.Run(async () =>
                        {
                            using (partition)
                            {
                                while (partition.MoveNext())
                                {
                                    var singleJob = await this.PaseSingleJob(jobName, partition.Current);
                                    resultBag.Add(singleJob);
                                }
                            }
                        })
                    )
                );

                return resultBag.Where(x => x != null).ToList();
            }

            return new List<BuildServerExecutionInfo>();
        }

        private async Task<BuildServerExecutionInfo> PaseSingleJob([NotNull] string jobName,
            Tuple<int, bool, string> updateJobInfo)
        {
            //http://208.0.0.1:8081/job/FbpfTestPromDump/249/api/xml
            //http://208.0.0.1:8081/job/AsudTestPromDump/247/api/xml?tree=building,displayName,fullDisplayName,changeSets[items[comment]]
            var request = this.CreateRequest("/job/" + jobName + "/" + updateJobInfo.Item1 +
                                             "/api/xml?tree=description,building,displayName,fullDisplayName,changeSets[items[comment]]");
            using (var response = (HttpWebResponse) (await request.GetResponseAsync()))
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                var execInfo = new BuildServerExecutionInfo();

                execInfo.JobName = jobName;
                execInfo.JobNum = updateJobInfo.Item1;
                execInfo.LastRefreshJob = DateTime.Now;
                switch (updateJobInfo.Item3)
                {
                    case "SUCCESS":
                        execInfo.Status = EnumJobStatus.Success;
                        break;
                    case "FAILURE":
                        execInfo.Status = EnumJobStatus.Fail;
                        break;
                    case "ABORTED":
                        execInfo.Status = EnumJobStatus.Aborted;
                        break;
                    default:
                        execInfo.Status = EnumJobStatus.Undef;
                        break;
                }

                if (updateJobInfo.Item2)
                    execInfo.Status = EnumJobStatus.InProgress;

                var xDoc = await XDocument.LoadAsync(response.GetResponseStream(), LoadOptions.None,
                    CancellationToken.None);

                var xRoot = xDoc.Element("workflowRun") ?? throw new NotSupportedException("Не нашли корень");


                var xDesc = xRoot.Element("description");
                if (xDesc != null)
                    execInfo.JobDescription = xDesc.Value;

                var xChangeSet = xRoot.Element("changeSet");
                if (xChangeSet != null)
                {
                    foreach (var xChSetItem in xChangeSet.Elements("item"))
                    {
                        var xChangeComment = xChSetItem.Element("comment");
                        if (xChangeComment != null)
                            execInfo.ChangeSet.Add(xChangeComment.Value.EscapeHtml());
                    }
                }

                return execInfo;
            }
        }

        private async Task RefreshJobs([NotNull] string jobName)
        {
            var newJobInfos = await this.GetJenkinsData(jobName);

            if (newJobInfos.Count != 0)
                _ = this.Logger.Info("Получено новых данных {Проект} {Количество}", jobName, newJobInfos.Count);

            // На всякий случай лочим, что б в параллель сюда не забраться. Но в newjob могу быть дубли.
            lock (this.AllExecInfos)
            {
                foreach (var executionInfo in newJobInfos)
                {
                    var addedExecInfo = this.AllExecInfos.FirstOrDefault(x =>
                        x.JobName == executionInfo.JobName && x.JobNum == executionInfo.JobNum);
                    if (addedExecInfo != null)
                    {
                        if (addedExecInfo.Status != executionInfo.Status)
                        {
                            addedExecInfo.CopyFrom(executionInfo);
                        }
                    }
                    else
                        this.AllExecInfos.Add(executionInfo);
                }
            }
        }

        /// <summary> Создание запроса к jenkins с безопасностью и т.д. </summary>
        [NotNull]
        private WebRequest CreateRequest([NotNull] string partAddress)
        {
            var request = WebRequest.Create(this._jenkinsAddress + (partAddress.StartsWith("/") ? "" : "/") + partAddress);
            this.JenkinsAuthFunc?.Invoke(request);
            request.Timeout = 5000000;
            return request;
        }

        public async Task<Dictionary<string, string>> GetVersion([NotNull] string projectName)
        {
            var reTry = 0;
            while (true)
            {
                try
                {
                    var res = await this.GetVersionPrivate(projectName);
                    return res;
                }
                catch (TaskCanceledException e)
                {
                    reTry++;
                    Console.WriteLine($"Ошибка получения версии для проекта {projectName}. Повтор: {reTry}. {e.InnerException?.Message}"); 
                    if (reTry > 4)
                    {
                        Console.WriteLine("Ошибка " + e);
                        throw;
                    }
                }
            }

        }

        private async Task<Dictionary<string, string>> GetVersionPrivate([NotNull] string projectName)
        {

            return new Dictionary<string, string>
            {
                { "Rc", "0" },
                { "Current", "0" },
                { "Prom", "0" },
            };

            //var htmlWeb = new HtmlAgilityPack.HtmlWeb();
            //htmlWeb.PreRequest = request =>
            //{
            //    request.Timeout = 5000000;
            //    string basicAuthToken = Convert.ToBase64String(Encoding.Default.GetBytes("bot:1"));
            //    request.Headers["Authorization"] = "Basic " + basicAuthToken;
            //    return true;
            //};
            //var htmlDoc = await htmlWeb.LoadFromWebAsync(this._jenkinsAddress + "configure");

            //var res = new Dictionary<string, string>();

            //var rc = htmlDoc.DocumentNode.SelectSingleNode("//*[@name='env.key' and @value = '" + projectName + "Rc']/../../..//*[@name='env.value']");
            //if (rc == null)
            //    throw new NotSupportedException($"Ошибка получения информации из jenkins. По префиксу {projectName} не найдено версии для rc");
            //res.Add("Rc", rc.GetAttributeValue("value", "0"));

            //var current = htmlDoc.DocumentNode.SelectSingleNode("//*[@name='env.key' and @value = '" + projectName + "Current']/../../..//*[@name='env.value']");
            //if (current == null)
            //    throw new NotSupportedException($"Ошибка получения информации из jenkins. По префиксу {projectName} не найдено версии для current");
            //res.Add("Current", current.GetAttributeValue("value", "0"));

            //var prom = htmlDoc.DocumentNode.SelectSingleNode("//*[@name='env.key' and @value = '" + projectName + "Prom']/../../..//*[@name='env.value']");
            //if (prom == null)
            //    throw new NotSupportedException($"Ошибка получения информации из jenkins. По префиксу {projectName} не найдено версии для prom");
            //res.Add("Prom", prom.GetAttributeValue("value", "0"));

            //return res;
        }
    }
}