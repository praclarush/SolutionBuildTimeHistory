using BuildTimeHistory.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildTimeHistory.Models
{
    public class DailyBuildHistory
    {
        public DateTime Date { get; set; }

        [JsonIgnore]
        public int TotalCount => BuildHistory.Count;

        [JsonIgnore]
        public int TotalSuccess => BuildHistory.Count(x => x.Status == BuildCompletionStatus.Succeeded);

        [JsonIgnore]
        public int TotalFailed => BuildHistory.Count(x => x.Status == BuildCompletionStatus.Failed);

        [JsonIgnore]
        public int TotalCancelled => BuildHistory.Count(x => x.Status == BuildCompletionStatus.Cancelled);

        public List<BuildHistoryItem> BuildHistory { get; set; }

        [JsonIgnore]
        public double AverageBuildTime => BuildHistory.Average(x => x.BuildTime);

        [JsonIgnore]
        public double TotalBuildTime => BuildHistory.Sum(x => x.BuildTime);

        public DailyBuildHistory()
        {
            Date = DateTime.Now;
            BuildHistory = new List<BuildHistoryItem>();
        }        
    }
}
