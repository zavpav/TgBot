using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Serilog;

namespace TgBot.Logger
{
    public class LoggerImp : ILog
    {
        [CanBeNull]
        private ILogger _logger;

        [NotNull]
        private ILogger Logger
        {
            get
            {
                if (this._logger == null)
                {
                    var loggerConfig = new Serilog.LoggerConfiguration();
                    loggerConfig.WriteTo.Console();
                    this._logger = loggerConfig.CreateLogger();
                }

                return this._logger ?? throw new Exception("Ошибка потоков");
            }
        }

        public void InfoSync(string messageTemplate, params object[] args)
        {
            this.Logger.Information(messageTemplate, args);
        }

        public Task Info(string messageTemplate, params object[] args)
        {
            return Task.Run(() => { this.Logger.Information(messageTemplate, args); });
        }
    }
}