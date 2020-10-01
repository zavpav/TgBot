namespace TgBot.Tg
{
    public static class TgHelper
    {
        public static string EscapeHtml(this string str)
        {
            str = str.Replace("<", "&lt;");
            str = str.Replace(">", "&gt;");
            
            return str;
        }
    }
}