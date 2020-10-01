using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TgBot.Project;

namespace TgBot.Bot.ConversationVersion
{
    public static class ConversationHelper
    {
        /// <summary> Сформировать строку с описанием версии </summary>
        [NotNull]
        public static async Task<string> GenerateVersionInfo([NotNull] IProject prj, [NotNull] IMainBot mainBot, [NotNull] string version)
        {
            // Тестировщик
            //    ⚽️
            // Программист
            //    🖥
            //Начальник
            //    💶
            //Аналитик
            //    📡

            var msg = $"<b>Версия по проекту {prj.Name} Текущая {version}</b>\n";
            var tasks = await mainBot.RedmineService.RedmineTasks(prj.RedmineProjectName, version);
            if (tasks.Count == 0)
                msg += "Задач не найдено";
            else
            {
                foreach (var status in tasks
                    .Select(x => x.Status)
                    .Distinct()
                    .OrderBy(x => 
                        x.ToLower() == "готов к работе" ? 1 :
                        x.ToLower() == "в работе" ? 3 :
                        x.ToLower() == "переоткрыт" ? 2 :
                        x.ToLower() == "на тестировании" ? 4 : 
                        x.ToLower() == "решен" ? 5 : 10
                        ))
                {
                    msg += $"\nСтатус <b>{status}</b>\n";

                    foreach (var task in tasks.Where(x => x.Status == status).OrderBy(x => x.Num))
                    {
                        msg += "\n";
                        var usr = mainBot.Users().FirstOrDefault(x => x.RedmineUser == task.AssignOn);
                        if (usr != null)
                        {
                            switch (usr.Role)
                            {
                                case EnumUserRole.Developer:
                                    msg += "🖥";
                                    break;
                                case EnumUserRole.Tester:
                                    msg += "⚽️";
                                    break;
                                case EnumUserRole.Boss:
                                    msg += "💶";
                                    break;
                                case EnumUserRole.Analist:
                                    msg += "📡";
                                    break;
                                default:
                                    msg += "㊙️";
                                    break;
                            }
                        }
                        else
                        {
                            msg += "㊙️";
                        }

                        msg += $"<a href=\"{mainBot.RedmineService.FormatTaskAddress(task.Num)}\">#{task.Num}</a> {task.Subject}\n";

                        //msg += "<i>Статус: " + task.Status + "</i>\n";
                        msg += "<i>Назначена: " + task.AssignOn + "</i>\n";
                    }

                }

            }

            return msg;


        }
    }
}