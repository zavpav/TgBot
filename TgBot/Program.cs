using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Binder;
using Microsoft.Extensions.Configuration.Json;
using Telegram.Bot;
using TgBot.AppConfiguration;
using TgBot.Bot;

namespace TgBot
{
    class Program
    {
        private static TelegramBotClient Bot;

        static async Task Main(string[] args)
        {
            var createMainBot = new Configure();

            var mainBot = createMainBot.Create();

            await mainBot.StartLoop();
        }
    }
}
