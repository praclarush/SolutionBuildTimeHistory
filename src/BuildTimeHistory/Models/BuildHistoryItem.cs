using BuildTimeHistory.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildTimeHistory.Models
{
    public class BuildHistoryItem
    {
        public DateTime RecordDate { get; set; }
        public BuildCompletionStatus Status { get; set; } = BuildCompletionStatus.Unknown;
        public double BuildTime { get; set; }
    }
}
