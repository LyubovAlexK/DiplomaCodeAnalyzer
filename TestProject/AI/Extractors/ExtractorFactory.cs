using Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Extractors
{
    public class ExtractorFactory
    {
        private readonly DictionaryService? _dict;
        private readonly string? _apiKey;

        public ExtractorFactory(DictionaryService? dict = null, string? apiKey = null)
        {
            _dict = dict;
            _apiKey = apiKey;
        }

        public IRequirementExtractor Create(bool isOnline)
        {
            if (isOnline)
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                    throw new InvalidOperationException("API-ключ не указан.");
                if (_dict == null)
                    throw new InvalidOperationException("DictionaryService не указан.");
                return new GigaChatExtractor(_apiKey, _dict);
            }

            if (_dict == null)
                throw new InvalidOperationException("DictionaryService не указан.");

            return new LocalRequirementExtractor(_dict);
        }
    }
}
