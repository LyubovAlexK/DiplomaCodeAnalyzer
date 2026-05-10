using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class GraphNode
    {
        public string Id { get; set; } = string.Empty;          // уникальный ID: "Namespace.ClassName"
        public string Name { get; set; } = string.Empty;        // отображаемое имя
        public string FullName { get; set; } = string.Empty;    // полное имя с namespace
        public string Kind { get; set; } = "Class";             // Class, Interface, Struct, Method
        public string Namespace { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public int MethodCount { get; set; }
        public double CyclomaticComplexity { get; set; }
        public bool HasViolations { get; set; }
        public string Category { get; set; } = "Other"; // Data / Business / UI / DataAccess / Other
    }
    public class GraphEdge
    {
        public string SourceId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string RelationType { get; set; } = "USES";      // USES, CALLS, EXTENDS, IMPLEMENTS
        public bool IsViolation { get; set; }
    }
    public class ProjectGraph
    {
        public List<GraphNode> Nodes { get; set; } = new();
        public List<GraphEdge> Edges { get; set; } = new();
        public int TotalClasses { get; set; }
        public int TotalMethods { get; set; }
        public List<string> CyclicDependencies { get; set; } = new();
    }
}
