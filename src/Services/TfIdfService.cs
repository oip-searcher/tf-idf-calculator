using System.Text;
using Common;

namespace TfIdfCalculator.Services;

public class TfIdfService(ILogger<TfIdfService> logger)
{
    private readonly Dictionary<string, int> _termDf = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lemmaDf = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _termToLemma = new(StringComparer.OrdinalIgnoreCase);

    private int _totalDocs;

    public void Run(
        string dataPath,
        string lemmasFile,
        string tokensPerDocPath,
        string lemmasPerDocPath,
        string outputDir)
    {
        ValidatePaths(dataPath, lemmasFile, tokensPerDocPath, lemmasPerDocPath);

        Directory.CreateDirectory(outputDir);

        logger.LogInformation("Шаг 1: загрузка term -> lemma mapping");
        LoadLemmasMapping(lemmasFile);

        logger.LogInformation("Шаг 2: сбор DF статистики");
        CollectDocumentFrequencies(tokensPerDocPath, lemmasPerDocPath);

        logger.LogInformation("Шаг 3: расчёт TF-IDF");
        CalculateTfIdfPerDocument(dataPath, outputDir);

        logger.LogInformation("TF-IDF расчёт завершён");
    }

    private static void ValidatePaths(
        string dataPath,
        string lemmasFile,
        string tokensPerDocPath,
        string lemmasPerDocPath)
    {
        if (!Directory.Exists(dataPath))
            throw new DirectoryNotFoundException($"DataPath not found: {dataPath}");

        if (!Directory.Exists(tokensPerDocPath))
            throw new DirectoryNotFoundException($"TokensPerDocPath not found: {tokensPerDocPath}");

        if (!Directory.Exists(lemmasPerDocPath))
            throw new DirectoryNotFoundException($"LemmasPerDocPath not found: {lemmasPerDocPath}");

        if (!File.Exists(lemmasFile))
            throw new FileNotFoundException($"LemmasFile not found: {lemmasFile}");
    }

    /// <summary>
    /// Формат lemmas.txt:
    /// lemma token1 token2 ... tokenN
    /// </summary>
    private void LoadLemmasMapping(string lemmasFile)
    {
        foreach (var line in File.ReadAllLines(lemmasFile, Encoding.UTF8))
        {
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            var lemma = parts[0].ToLowerInvariant();

            for (var i = 1; i < parts.Length; i++)
            {
                var term = parts[i].ToLowerInvariant();
                _termToLemma.TryAdd(term, lemma);
            }
        }

        logger.LogInformation("Загружено маппингов term->lemma: {Count}", _termToLemma.Count);
    }

    private void CollectDocumentFrequencies(string tokensPerDocPath, string lemmasPerDocPath)
    {
        _termDf.Clear();
        _lemmaDf.Clear();

        var tokenFiles = Directory.GetFiles(tokensPerDocPath, "*_tokens.txt")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _totalDocs = tokenFiles.Count;

        foreach (var file in tokenFiles)
        {
            var uniqueTerms = File.ReadAllLines(file, Encoding.UTF8)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var term in uniqueTerms)
            {
                _termDf.TryAdd(term, 0);
                _termDf[term]++;
            }
        }

        var lemmaFiles = Directory.GetFiles(lemmasPerDocPath, "*_lemmas.txt")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in lemmaFiles)
        {
            var uniqueLemmas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
            {
                var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                var lemma = parts[0].ToLowerInvariant();
                uniqueLemmas.Add(lemma);
            }

            foreach (var lemma in uniqueLemmas)
            {
                _lemmaDf.TryAdd(lemma, 0);
                _lemmaDf[lemma]++;
            }
        }

        logger.LogInformation("Обработано документов: {Count}", _totalDocs);
        logger.LogInformation("Уникальных терминов: {Count}", _termDf.Count);
        logger.LogInformation("Уникальных лемм: {Count}", _lemmaDf.Count);
    }

    private static List<string> ExtractTermsFromFile(string filePath)
    {
        var text = TextProcessor.ExtractTextFromHtmlFile(filePath);
        return TextProcessor.Tokenize(text, minTokenLength: 3).ToList();
    }

    private void CalculateTfIdfPerDocument(string dataPath, string outputDir)
    {
        var files = Directory.GetFiles(dataPath, "*.txt")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var terms = ExtractTermsFromFile(file);
            var totalTerms = terms.Count;

            if (totalTerms == 0)
            {
                logger.LogWarning("Документ {File} пуст после токенизации", fileName);
                continue;
            }

            var termFreq = terms
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            SaveTermsTfIdf(fileName, termFreq, totalTerms, outputDir);
            SaveLemmasTfIdf(fileName, termFreq, totalTerms, outputDir);

            logger.LogInformation("Обработан документ {File}", fileName);
        }
    }

    private void SaveTermsTfIdf(
        string fileName,
        Dictionary<string, int> termFreq,
        int totalTerms,
        string outputDir)
    {
        var lines = new List<string>();

        foreach (var (term, count) in termFreq)
        {
            var tf = (double)count / totalTerms;
            var idf = CalculateIdf(term, _termDf);
            var tfidf = tf * idf;

            lines.Add($"{term} {idf:F6} {tfidf:F6}");
        }

        File.WriteAllLines(
            Path.Combine(outputDir, $"{fileName}_terms.txt"),
            lines.OrderBy(l => l.Split(' ')[0], StringComparer.OrdinalIgnoreCase),
            Encoding.UTF8);
    }

    private void SaveLemmasTfIdf(
        string fileName,
        Dictionary<string, int> termFreq,
        int totalTerms,
        string outputDir)
    {
        var lemmaFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (term, count) in termFreq)
        {
            if (!_termToLemma.TryGetValue(term, out var lemma))
                continue;

            lemmaFreq.TryAdd(lemma, 0);
            lemmaFreq[lemma] += count;
        }

        var lines = new List<string>();

        foreach (var (lemma, count) in lemmaFreq)
        {
            var tf = (double)count / totalTerms;
            var idf = CalculateIdf(lemma, _lemmaDf);
            var tfidf = tf * idf;

            lines.Add($"{lemma} {idf:F6} {tfidf:F6}");
        }

        File.WriteAllLines(
            Path.Combine(outputDir, $"{fileName}_lemmas.txt"),
            lines.OrderBy(l => l.Split(' ')[0], StringComparer.OrdinalIgnoreCase),
            Encoding.UTF8);
    }

    /// <summary>
    /// Формула IDF: log(N / (1 + df))
    /// </summary>
    private double CalculateIdf(string term, Dictionary<string, int> dfMap)
    {
        if (_totalDocs == 0)
            return 0;

        if (!dfMap.TryGetValue(term, out var df))
            return 0;

        return Math.Log(_totalDocs / (1.0 + df));
    }
}