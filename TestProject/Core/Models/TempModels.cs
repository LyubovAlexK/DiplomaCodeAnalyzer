using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class TempModels
    {
        //Модуль 4 Project Validator

        //Результат проверки на пустой проект
        public class EmptyProjectResult
        {
            public bool IsEmpty { get; set; }
            public string Reason { get; set; } = string.Empty;
            public int TotalMethods { get; set; }
            public int TotalClasses { get; set; }
            public int UserClasses { get; set; }
            public int MeaningfulMethods { get; set; }
            public int TemplateMatches { get; set; }
            public int TotalFiles { get; set; }
            public int FailLevel { get; set; }
            public double Confidence { get; set; }
        }

        //Модуль 5 NetArchTest

        //Результат проверки одного архитектурного правила
        public class ArchViolation
        {
            public string RuleName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public bool IsPassed { get; set; }
        }
    }
}
