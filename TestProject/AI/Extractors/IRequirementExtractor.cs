using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AI.Extractors
{
    public interface IRequirementExtractor
    {
        Task<ProjectSpecification> ExtractAsync(string filePath, int specificationId = 0);
    }
}
