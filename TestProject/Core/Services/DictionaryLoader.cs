using Core.Data;
using Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services
{
    public class DictionaryLoader
    {
        private readonly AppDbContext _db;

        public DictionaryLoader(AppDbContext db)
        {
            _db = db;
        }
        public async Task<int> LoadFromFileAsync(string filePath, int minLength = 3)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл словаря не найден.", filePath);

            var lines = await File.ReadAllLinesAsync(filePath);
            var existingWords = await _db.SeparatorWords
                .Select(w => w.Word)
                .ToListAsync();

            var existingSet = new HashSet<string>(existingWords, StringComparer.OrdinalIgnoreCase);
            int added = 0;

            foreach (var line in lines)
            {
                var word = line.Trim().ToLower();

                // Пропускаем: короткие, с пробелами, с цифрами, с латиницей (по желанию)
                if (word.Length < minLength) continue;
                if (word.Contains(' ')) continue;
                if (word.Any(char.IsDigit)) continue;
                // if (word.Any(c => c >= 'a' && c <= 'z')) continue; // только кириллица (раскомментировать если нужно)

                if (!existingSet.Contains(word))
                {
                    _db.SeparatorWords.Add(new SeparatorWord { Word = word });
                    existingSet.Add(word);
                    added++;
                }
            }

            await _db.SaveChangesAsync();
            return added;
        }
    }
}
