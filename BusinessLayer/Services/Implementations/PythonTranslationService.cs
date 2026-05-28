using Application.Interfaces;
using System.Diagnostics;
using System.Text.Json;

namespace BusinessLayer.Services.Implementations;

public class PythonTranslationService : ITranslationService
{
    // C# proqramının işə salacağı Python icra faylının mütləq yolu
    private const string PythonExecutablePath = @"C:\Users\user\AppData\Local\Programs\Python\Python312\python.exe";

    // Tərcüməni həyata keçirən Python skriptinin mütləq yolu
    private readonly string _pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "translator.py");
    public async Task<Dictionary<string, string>> TranslateTextAsync(string text, List<string> targetLangs)
    {
        var translations = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(text) || targetLangs == null || targetLangs.Count == 0) return translations;

        var targetLangsStr = string.Join(",", targetLangs);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                // PYTHON İCRA FAYLININ YOLU
                FileName = PythonExecutablePath,

                // Arqumentlər: 1. Skriptin yolu, 2. Tərcümə mətni, 3. Hədəf dillər
                Arguments = $"\"{_pythonScriptPath}\" \"{text.Replace("\"", "\\\"")}\" \"{targetLangsStr}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Console.WriteLine("PYTHON ERROR: Proses başlaya bilmədi (Yol xətası).");
                    return translations;
                }

                string resultJson = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    // Python-dan gələn xəta (məsələn, googletrans bloklanıb)
                    Console.WriteLine($"PYTHON ERROR OUTPUT: {error}");
                    return translations;
                }

                if (string.IsNullOrEmpty(resultJson) || resultJson.Contains("Error"))
                {
                    // Python-dan gələn boş cavab və ya qeyri-düzgün format
                    Console.WriteLine("TRANSLATION FAIL: Boş və ya xətalı JSON cavabı.");
                    return translations;
                }

                // JSON-u Dictionary-ə çeviririk
                var result = JsonSerializer.Deserialize<Dictionary<string, string>>(resultJson);

                if (result != null && result.Any())
                {
                    Console.WriteLine("TRANSLATION SUCCESS: Məqalə uğurla tərcümə edildi.");
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL C# ERROR (Python Process): {ex.Message}. Check if Python file exists.");
            return translations;
        }
        return translations;
    }
}
