using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Extractors
{
    public class ExtractorFactory
    {
        private readonly string? _deepSeekApiKey;

        public ExtractorFactory(string? deepSeekApiKey = null)
        {
            _deepSeekApiKey = deepSeekApiKey;
        }

        public IRequirementExtractor Create(bool isOnline)
        {
            if (isOnline)
            {
                if (string.IsNullOrWhiteSpace(_deepSeekApiKey))
                    throw new InvalidOperationException("API-ключ DeepSeek не указан.");

                return new DeepSeekRequirementExtractor(_deepSeekApiKey);
            }

            return new LocalRequirementExtractor();
        }
    }
}
