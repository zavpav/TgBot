namespace TgBot.BuildPuller
{
    /// <summary> Пинальщик билд серверов </summary>
    public interface IBuildPuller
    {
        /// <summary> Пнуть по поводу новостей билд сервер </summary>
        void PullNews();
    }
}