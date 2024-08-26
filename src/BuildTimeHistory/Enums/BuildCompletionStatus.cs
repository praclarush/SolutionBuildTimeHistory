using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildTimeHistory.Enums
{
    public enum BuildCompletionStatus
    {
        Unknown = 0,
        Succeeded = 1,
        Failed = 2,
        Cancelled = 3
    }
}
