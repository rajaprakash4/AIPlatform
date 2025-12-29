using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIPlatform.Core.Models
{
    public class WorkflowResult
    {
        public Dictionary<string, object> FinalContext { get; set; }
        public List<StepTrace> Trace { get; set; } = new();
    }

    public class StepTrace
    {
        public string ToolId { get; set; }
        public Dictionary<string, object> Input { get; set; }
        public Dictionary<string, object> Output { get; set; }
        public double DurationMs { get; set; }
        public bool Success { get; set; }
    }
}
