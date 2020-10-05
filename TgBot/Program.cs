using System.Threading.Tasks;
using TgBot.Bot;

namespace TgBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var createMainBot = new Configure();

            var mainBot = createMainBot.Create();

            await mainBot.StartLoop();
        }
    }
}
