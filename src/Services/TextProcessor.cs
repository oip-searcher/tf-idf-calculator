using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Common;

/// <summary>
/// Общие методы для извлечения текста из HTML и токенизации русского текста.
/// </summary>
public static class TextProcessor
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "и", "в", "во", "не", "что", "он", "на", "я", "с", "со", "как", "а", "то",
        "все", "она", "так", "его", "но", "да", "ты", "к", "у", "же", "вы", "за",
        "бы", "по", "только", "ее", "мне", "было", "вот", "от", "меня", "еще", "нет",
        "о", "из", "ему", "теперь", "когда", "даже", "ну", "вдруг", "ли", "если",
        "уже", "или", "ни", "быть", "был", "него", "до", "вас", "нибудь", "опять",
        "уж", "вам", "ведь", "там", "потом", "себя", "ничего", "ей", "может", "они",
        "тут", "где", "есть", "надо", "ней", "для", "мы", "тебя", "их", "чем",
        "была", "сам", "чтоб", "без", "будто", "чего", "раз", "тоже", "себе", "под",
        "будет", "ж", "тогда", "кто", "этот", "того", "потому", "этого", "какой",
        "совсем", "ним", "здесь", "этом", "один", "почти", "мой", "тем", "чтобы",
        "нее", "сейчас", "были", "куда", "зачем", "всех", "никогда", "можно", "при",
        "наконец", "два", "об", "другой", "хоть", "после", "над", "больше", "тот",
        "через", "эти", "нас", "про", "всего", "них", "какая", "много", "разве",
        "три", "эту", "моя", "впрочем", "хорошо", "свою", "этой", "перед", "иногда",
        "лучше", "чуть", "том", "нельзя", "такой", "им", "более", "всегда", "конечно",
        "всю", "между"
    };
    
    public static string ExtractTextFromHtmlFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Путь к файлу не задан.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("HTML-файл не найден.", filePath);

        var html = File.ReadAllText(filePath, Encoding.UTF8);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        RemoveNodes(doc, "//script|//style|//noscript");

        var text = doc.DocumentNode.InnerText ?? string.Empty;
        text = WebUtility.HtmlDecode(text);

        return NormalizeWhitespace(text);
    }
    
    public static IEnumerable<string> Tokenize(string text, int minTokenLength = 3)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Enumerable.Empty<string>();

        var normalized = text
            .Replace('Ё', 'Е')
            .Replace('ё', 'е')
            .ToLowerInvariant();

        // Только русские слова, включая дефисные
        var matches = Regex.Matches(
            normalized,
            @"[а-я]+(?:-[а-я]+)*",
            RegexOptions.CultureInvariant);

        return matches
            .Select(m => m.Value)
            .Where(t => t.Length >= minTokenLength)
            .Where(t => !StopWords.Contains(t));
    }

    /// <summary>
    /// Удаляет из HTML ненужные узлы по XPath.
    /// </summary>
    private static void RemoveNodes(HtmlDocument doc, string xpath)
    {
        var nodes = doc.DocumentNode.SelectNodes(xpath);
        if (nodes is null)
            return;

        foreach (var node in nodes)
            node.Remove();
    }
    
    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}