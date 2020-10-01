using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Autofac;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Telegram.Bot;
using TgBot.AppConfiguration;
using TgBot.Logger;
using TgBot.Project;
using TgBot.Services;
using TgBot.Tg;

namespace TgBot.Bot
{
    /// <summary> Конфигурирует и создаёт основного бота </summary>
    public class Configure
    {
        /// <summary> Собственно создание бота </summary>
        public IMainBot Create()
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.my.json");
            configurationBuilder.AddJsonFile("appsettings.projects.json");
            var config = configurationBuilder.Build();
            var appConf = config
                .GetSection("App")
                .Get<AppConfiguration.AppConfiguration>(o => o.BindNonPublicProperties = false);
            
            Debug.Assert(appConf.Services != null, "appConf.Services != null");
            var servicesConfiguration = appConf.Services;

            Debug.Assert(servicesConfiguration.Tg != null, "appConf.Tg != null (App:Services:Tg section in json)");
            Debug.Assert(servicesConfiguration.Tg.Proxy != null, "appConf.Tg.Proxy != null (App:Services:Tg:Proxy section in json)");
            Debug.Assert(servicesConfiguration.Tg.Proxy.NetworkCredential != null, "appConf.Tg.Proxy.NetworkCredential != null (App:Services:Tg:Proxy:NetworkCredential section in json)");
            var proxy = new WebProxy(servicesConfiguration.Tg.Proxy.Host, servicesConfiguration.Tg.Proxy.Port);
            proxy.Credentials = new NetworkCredential(servicesConfiguration.Tg.Proxy.NetworkCredential.Name, servicesConfiguration.Tg.Proxy.NetworkCredential.Password);
            var tgClient = new TelegramBotClient(servicesConfiguration.Tg.Key, webProxy: proxy);



            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterType<LoggerImp>().As<ILog>().SingleInstance();
            containerBuilder.RegisterType<JenkinsService>().As<IJenkinsService>().SingleInstance();
            containerBuilder.RegisterType<GitService>().As<IGitService>().SingleInstance();
            containerBuilder.RegisterType<RedmineService>().As<IRedmineService>().SingleInstance();
            containerBuilder.RegisterType<MainBot>().As<IMainBot>().SingleInstance();
            containerBuilder.RegisterType<TgClientService>().WithParameter(new NamedParameter("tgClient", tgClient)).As<ITgClientService>().SingleInstance();
            containerBuilder.RegisterType<JenkinsProject>().AsSelf();

            containerBuilder.RegisterInstance(appConf).SingleInstance();

            var container = containerBuilder.Build();


            var mainBot = container.Resolve<IMainBot>();
            
            this.FillJenkinsProjects(mainBot, appConf.Projects, nm => container.Resolve<JenkinsProject>(new NamedParameter("name", nm)));
            this.FillUsers(mainBot);

            return mainBot;
        }

        /// <summary> Заполнить пользователей </summary>
        private void FillUsers([NotNull] IMainBot mainBot)
        {
            var ldUsers = JsonConvert.DeserializeObject<List<User>>(File.ReadAllText("app.usersettings.json"));
            foreach (var user in ldUsers)
            {
                mainBot.AddUser(user);
            }
        }

        /// <summary> Заполнить описание проектов в боте </summary>
        private void FillJenkinsProjects([NotNull] IMainBot mainBot,
                [NotNull] List<ProjectConfiguration> appConfigurationProjects, 
                [NotNull] Func<string, JenkinsProject> jenkinsProjectFunc)
        {
            foreach (var projectConfiguration in appConfigurationProjects)
            {
                var prj = jenkinsProjectFunc(projectConfiguration.ProjectName ?? throw new NotSupportedException("Ошибка конфигурации ProjectName"));
                if (projectConfiguration.Builds != null)
                    foreach (var buildConfiguration in projectConfiguration.Builds)
                    {
                        prj.AddBuildPuller(buildConfiguration.Desc ?? throw new NotSupportedException("Ошибка конфигурации ProjectBuild:Desc"), 
                            buildConfiguration.Job ?? throw new NotSupportedException("Ошибка конфигурации ProjectBuild:Job"), 
                            buildConfiguration.JobType);
                    }

                prj.JenkinsPrefix = projectConfiguration.JenkinsPrefix;
                if (projectConfiguration.RedmineProject != null)
                    prj.SetRedmineInfo(projectConfiguration.RedmineProject);

                mainBot.AddProject(prj);
            }
        }
    }
}