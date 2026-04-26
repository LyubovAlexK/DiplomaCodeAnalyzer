using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Extractors
{
    public class ExtractorFactory
    {
        public IRequirementExtractor Create(bool isOnline)
        {
            if (isOnline)
            {
                throw new NotImplementedException("DeepSeek-анализатор ещё не реализован.");
            }
            return new LocalRequirementExtractor();
        }
    }
}
