using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface ITranslationService
    {
        // Text-i Azərbaycan dilindən targetLangs (en, ru, ar) siyahısına tərcümə edir
        Task<Dictionary<string, string>> TranslateTextAsync(string text, List<string> targetLangs);
    }
}