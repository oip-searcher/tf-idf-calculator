namespace TfIdfCalculator.Options;

public class TfIdfOptions
{
    public string DataPath { get; set; } = "pages";
    public string LemmasFile { get; set; } = "lemmas.txt";
    public string TokensPerDocPath { get; set; } = "tokens_per_doc";
    public string LemmasPerDocPath { get; set; } = "lemmas_per_doc";
    public string OutputDir { get; set; } = "tfidf_output";
}