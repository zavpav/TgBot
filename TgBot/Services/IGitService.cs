using System.Collections.Generic;
using System.Threading.Tasks;

namespace TgBot.Services
{
    /// <summary> Взаимодействие с git </summary>
    public interface IGitService
    {
        Task Pull();
        
        Task<IEnumerable<string>> GetComments(string lastBuildCommitHash);
    }
}