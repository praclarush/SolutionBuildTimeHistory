using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildTimeHistory.Models
{
    public class SolutionBuildHistory
    {
        public string SolutionName { get; set; }

        public int DaysToKeep { get; set; } = 0;

        public DateTime LastUpdated { get; set; }

        public DateTime DateCreated { get; set; }

        public List<DailyBuildHistory> DailyBuildHistory { get; set; }

        public SolutionBuildHistory()
        {
            DailyBuildHistory = new List<DailyBuildHistory>();
            DateCreated = DateTime.Now;
            LastUpdated = DateTime.Now;
        }
    }
}
