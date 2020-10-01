using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using JetBrains.Annotations;
using TgBot.Bot;
using TgBot.Tg;

namespace TgBot.Services
{
    /// <summary> Взаимодействие с Redmine </summary>
    public class RedmineService : IRedmineService
    {
        [NotNull] 
        private IMainBot MainBot { get; }

        public RedmineService([NotNull] IMainBot mainBot, [NotNull] AppConfiguration.AppConfiguration configuration)
        {
            this.MainBot = mainBot;
            this.ServerAddress = configuration.Services?.Redmine?.Address ?? throw new NotSupportedException("Ошибка конфигурации. Не задан redmine");

            var usr = configuration.Services?.Redmine?.NetworkCredential?.Name;
            var psw = configuration.Services?.Redmine?.NetworkCredential?.Password;
            if (usr == null || psw == null)
                throw new NotSupportedException("Ошибка конфигурации. Не задан пользователь redmine");
            this.NetCredintal = new NetworkCredential(usr, psw);

            this.TasksCache = new List<RedmineTaskDesc>();
        }

        private NetworkCredential NetCredintal { get; set; }

        private List<RedmineTaskDesc> TasksCache { get; }

        public string ServerAddress { get; }

        private NetworkCredential GetCredential()
        {
            return this.NetCredintal;
        }

        private async Task<XDocument> ExecuteRequest(string addressPart)
        {
            var req = WebRequest.Create(this.ServerAddress + addressPart);
            req.Credentials = this.GetCredential();

            var  xResponse = await req.ExecuteRequest();
            return xResponse;
        }

        public RedmineTaskDesc TryGetTaskDescription(string taskNum)
        {
            lock (this.TasksCache)
            {
                return this.TasksCache.FirstOrDefault(x => x.Num == taskNum) ?? new RedmineTaskDesc(){Num = taskNum, Subject = "<Не обновлено, если будет часто - надо будет порешать>"};
            }
        }

        public string FormatTaskAddress(string taskNum)
        {
            return this.ServerAddress + "/issues/" + taskNum;
        }

        public async Task UpdateTasks()
        {
            //http://{srm}/projects/{proj}/issues.xml?fixed_version_id=134 Фильтр по версии

            var rqr = "issues.xml?status_id=*&sort=updated_on:desc&limit=1000";

            var lastUpdate = this.TasksCache.OrderByDescending(x => x.UpdateOn).FirstOrDefault();
            if (lastUpdate != null)
                rqr += "&updated_on=>%3D" + lastUpdate.UpdateOn.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var xLastUpdates = await this.ExecuteRequest(rqr);

            var messages = new List<IBotMessage>();
            lock (this.TasksCache)
            {
                var updatedTasks = this.CreateTaskDescFromXml(xLastUpdates.Root ?? throw new NotSupportedException("Нет рута"));
                var updatePair = updatedTasks
                        .Select(x => new  {NewTask = x, OldTask = this.TasksCache.SingleOrDefault(xx => xx.Num == x.Num)})
                        .ToList();

                foreach (var tsks in updatePair.Where(x => x.OldTask != null).Select(x => x.OldTask))
                    this.TasksCache.Remove(tsks);

                foreach (var tsks in updatePair.Select(x => x.NewTask))
                    this.TasksCache.Add(tsks);

                foreach (var msgPair in updatePair)
                {
                    if (msgPair.OldTask != null)
                    {
                        if (msgPair.NewTask.Subject == msgPair.OldTask.Subject
                            && msgPair.NewTask.Description == msgPair.OldTask.Description
                            && msgPair.NewTask.AssignOn == msgPair.OldTask.AssignOn
                            && msgPair.NewTask.Status == msgPair.OldTask.Status
                            && msgPair.NewTask.Resolution == msgPair.OldTask.Resolution
                            && msgPair.NewTask.Version == msgPair.OldTask.Version
                            && msgPair.NewTask.Project == msgPair.OldTask.Project
                        )
                        {
                            continue;
                        }
                    }

                    var msg = "<b>" + (msgPair.OldTask != null ? "Обновился" : "Появился новый") + " таск</b>\n";
                    msg += $"<a href=\"{this.FormatTaskAddress(msgPair.NewTask.Num)}\">#{msgPair.NewTask.Num}</a> {msgPair.NewTask.Subject}\n\n\n";

                    msg += "Статус: " + msgPair.NewTask.Status + "\n";
                    msg += "Назначена: " + msgPair.NewTask.AssignOn + "\n";

                    var botMsg = new BotMessageRedmine(msg)
                    {
                        RdmAssignTo = msgPair.NewTask.AssignOn,
                        RdmProject = msgPair.NewTask.Project,
                        RdmPrevAssignTo = msgPair.OldTask?.AssignOn ?? ""
                    };
                    messages.Add(botMsg);
                }
            }

            var tasks = messages.Select(x => this.MainBot.ProcessInternalMessage(x, true)).ToList();
            Task.WaitAll(tasks.ToArray());
        }

        public async Task<List<RedmineTaskDesc>> RedmineTasks(string redmineProjectName, string versionName)
        {
            //http://{srm}/projects/{proj}/versions.xml
            var xVersions = await this.ExecuteRequest($"projects/{redmineProjectName.ToLower()}/versions.xml");

            var allProjectVersions = new List<Tuple<string, string>>();
            foreach (var xVer in xVersions.Root.Elements("version"))
            {
                var verName = xVer.Element("name")?.Value;
                var verId = xVer.Element("id")?.Value;
                allProjectVersions.Add(Tuple.Create(verId, verName));
            }

            var id = allProjectVersions.SingleOrDefault(x => x.Item2 == versionName);

            if (id == null)
                return new List<RedmineTaskDesc>();

            //http://{srm}/issues.xml?fixed_version_id=134 Фильтр по версии

            var xRdmTasks = await this.ExecuteRequest("issues.xml?fixed_version_id=" + id.Item1);
            Debug.Assert(xRdmTasks.Root != null, "xRdmTasks.Root != null");
            return this.CreateTaskDescFromXml(xRdmTasks.Root);
        }

        [NotNull, ItemNotNull]
        private List<RedmineTaskDesc> CreateTaskDescFromXml([NotNull] XElement xResponse)
        {
            var tasks = new List<RedmineTaskDesc>();

            foreach (var xTask in xResponse.Elements("issue"))
            {
                var taskDesc = new RedmineTaskDesc
                {
                    Num = xTask.Element("id")?.Value,
                    Project = xTask.Element("project")?.Attribute("name")?.Value,
                    Status = xTask.Element("status")?.Attribute("name")?.Value,
                    AssignOn = xTask.Element("assigned_to")?.Attribute("name")?.Value,
                    Version = xTask.Element("fixed_version")?.Value,
                    Subject = xTask.Element("subject")?.Value.EscapeHtml(),
                    Description = xTask.Element("description")?.Value.EscapeHtml()
                };

                var updateOnStr = xTask.Element("updated_on")?.Value;
                if (updateOnStr != null)
                {
                    //2020-03-18T12:48:35Z
                    var dt = DateTime.ParseExact(updateOnStr, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                    taskDesc.UpdateOn = dt;
                }

                var resolution = xTask.Element("custom_fields")
                    ?.Elements("custom_field")
                    ?.Where(x => x.Attribute("name")?.Value == "Резолюция")
                    ?.FirstOrDefault()
                    ?.Value;

                if (resolution != null)
                    taskDesc.Resolution = resolution;

                tasks.Add(taskDesc);
            }

            return tasks;
        }

    }
}