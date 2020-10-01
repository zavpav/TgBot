using System;

namespace TgBot.Services
{
    public class RedmineTaskDesc
    {
        public string Num { get; set; }
        
        public string Subject { get; set; }

        public string Description { get; set; }

        public DateTime UpdateOn { get; set; }

        public string AssignOn { get; set; }
        
        public string Status { get; set; }

        public string Version { get; set; }
        
        public string Project { get; set; }
        
        public string Resolution { get; set; }
    }
}