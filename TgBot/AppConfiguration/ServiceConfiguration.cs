﻿using System.Collections.Generic;
using JetBrains.Annotations;

namespace TgBot.AppConfiguration
{
    public class ServiceConfiguration
    {
        [CanBeNull]
        public TgConfiguration Tg { get; set; }
        
        [CanBeNull]
        public RedmineConfiguration Redmine { get; set; }

        [CanBeNull]
        public JenkinsConfiguration Jenkins { get; set; }

        [CanBeNull]
        public List<GitConfiguration> Gits { get; set; }
    }
}