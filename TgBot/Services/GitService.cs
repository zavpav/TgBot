
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TgBot.Logger;

namespace TgBot.Services
{
    /// <summary> Взаимодействие с git </summary>
    public class GitService : IGitService
    {
        [NotNull] public ILog Logger { get; }

        public GitService([NotNull] ILog logger, [NotNull] AppConfiguration.AppConfiguration appConfiguration)
        {
            Logger = logger;
            var gitConfiguration = appConfiguration.Services?.Gits?[0] ?? throw new NotSupportedException("Ошибка конфигурации git");

            this.Bin = gitConfiguration.Bin ?? throw new NotSupportedException("Ошибка конфигурации git");
            this.Dir = gitConfiguration.Dir ?? throw new NotSupportedException("Ошибка конфигурации git");
            this.Repository = gitConfiguration.Repository ?? throw new NotSupportedException("Ошибка конфигурации git");
        }

        /// <summary> Путь к git.exe </summary>
        [NotNull]
        private string Bin { get; }

        /// <summary> Директория для проекта </summary>
        [NotNull]
        private string Dir { get; }

        /// <summary> git repository для pull </summary>
        [NotNull]
        private string Repository { get; set; }

        private DateTime LastUpdate { get; set; }
//cfa7130bf35b578c873f17e480884d6700442798 Merge branch

        private readonly Regex _commitRowComment = new Regex(@"^[A-Za-z0-9]{40}\s+(?<cmm>.+)$");

        public async Task Pull()
        {
            await GetComments("6b7dad96ad0c872b588f4b44cdca97cae0c69a92");
            await GetComments("5a25a64a8da0ef84b79a35a108a657fb00fc0729");
        }

        public async Task<IEnumerable<string>> GetComments([NotNull] string commitHash)
        {
            await this.UpdateGitDirectory();

            var commentLines = new HashSet<string>();
            var output = await this.ExecuteGit($"log {commitHash}..master --pretty=oneline");
            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("error: cannot spawn less: No such file or directory"))
                    continue;
                if (line.Contains("Merge branch"))
                    commentLines.Add(""); // Что б удоноее было определять "хоть какой-то коммит"
                var mch = this._commitRowComment.Match(line);
                if (mch.Success)
                    commentLines.Add(mch.Groups["cmm"].Value);
            }

            return commentLines;
        }

        /// <summary> Обновить кеш гита </summary>
        private async Task UpdateGitDirectory()
        {
            if (DateTime.Now - this.LastUpdate > TimeSpan.FromMinutes(2))
                return;

            this.LastUpdate = DateTime.Now;

            var output = await this.ExecuteGit($"pull {this.Repository} master");
            await this.Logger.Info("Обновление гита\n{Данные}", output);
        }

        /// <summary> Дёрнуть git </summary>
        private async Task<string> ExecuteGit([NotNull] string arguments)
        {
            var pProcess = new System.Diagnostics.Process();
            pProcess.StartInfo.FileName = this.Bin;
            pProcess.StartInfo.Arguments = arguments;
            pProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.RedirectStandardOutput = true;
            pProcess.StartInfo.WorkingDirectory = this.Dir;
            pProcess.Start();
            var output = await pProcess.StandardOutput.ReadToEndAsync();
            pProcess.WaitForExit();
            return output;
        }
    }
}