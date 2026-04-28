using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Extractors
{
    public class ExtractorFactory
    {
        private readonly string? _apiKey;

        public ExtractorFactory(string? apiKey = null)
        {
            _apiKey = apiKey;
        }

        public IRequirementExtractor Create(bool isOnline)
        {
            if (isOnline)
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                    throw new InvalidOperationException("API-ключ не указан.");

                return new GigaChatExtractor(_apiKey);
            }

            return new LocalRequirementExtractor();
        }
    }
}
