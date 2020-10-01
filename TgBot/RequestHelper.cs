using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TgBot
{
    public static class RequestHelper
    {
        public static async Task<XDocument> ExecuteRequest(this WebRequest request)
        {
            var reTry = 0;
            Exception ex = null;


            while (reTry < 3)
            {
                try
                {
                    using (var response = (HttpWebResponse) (await request.GetResponseAsync()))
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                            return await XDocument.LoadAsync(response.GetResponseStream(), LoadOptions.None, CancellationToken.None);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Ошибка выполнения запроса " + request.RequestUri + " ошибка " + e);
                    ex = e;
                }

                await Task.Delay(1000);

                reTry++;
            }

            if (reTry == 3)
            {
                if (ex != null)
                    throw ex; // Выкидываем последнюю ошибку... вообще - пофиг что - бот валится
                else
                    throw new NotSupportedException("Ошибка получения данных");
            }

            return null;
        }
    }
}