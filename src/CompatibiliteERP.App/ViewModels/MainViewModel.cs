using CompatibiliteERP.App.Data;
using CompatibiliteERP.App.Services;
using CompatibiliteERP.Core.Models;
using CompatibiliteERP.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace CompatibiliteERP.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private Compatibilite? _selectedCompatibility;
    private string _searchText = "";
    private string _status = "Données en cours de chargement.";
    private readonly List<Article> _allArticles = new();

    public ObservableCollection<Article> Articles { get; } = new();
    public ObservableCollection<Article> SearchResults { get; } = [];
    public ObservableCollection<Compatibilite> Compatibilities { get; } = new();

    private readonly SqlArticleRepository _repository;
    private readonly DesignationParser _parser = new();

    public MainViewModel()
    {
        _repository = new SqlArticleRepository(
            SqlConfiguration.ConnectionString);
    }

    public async Task LoadArticlesAsync()
    {
        try
        {
            var articles = (await _repository.LoadArticlesAsync()).ToList();

            for (int i = 0; i < articles.Count; i++)
            {
                var article = articles[i];

                if (article.Id == 0)
                    article.Id = i + 1;

                var (
                    caracteristiques,
                    confidence) = _parser.Extract(
                        article.Designation);

                article.Famille = article.Famille.Trim();
                article.Caracteristiques = caracteristiques;
                article.ConfianceExtraction = confidence;
                article.DesignationNormalisee =
                    DesignationNormalizer.Normalize(article.Designation);
            }

            SetArticles(articles);

            Status =
                $"{Articles.Count} référence(s) chargée(s) depuis SQL Server.";
        }
        catch (Exception ex)
        {
            Status = $"Erreur SQL : {ex.Message}";
        }
    }

    public Compatibilite? SelectedCompatibility
    {
        get => _selectedCompatibility;
        set
        {
            _selectedCompatibility = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImageCompatibiliteSelectionnee));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public void SetArticles(IEnumerable<Article> articles)
    {
        if (articles == null)
            throw new ArgumentNullException(nameof(articles));

        _allArticles.Clear();
        _allArticles.AddRange(articles);

        Articles.Clear();

        foreach (var article in _allArticles)
            Articles.Add(article);

        UpdateSearchResults(_allArticles);
    }

    private void UpdateSearchResults(IEnumerable<Article> articles)
    {
        SearchResults.Clear();

        foreach (var article in articles.Take(50))
            SearchResults.Add(article);

        SelectedArticle = SearchResults.FirstOrDefault();
    }

    private static readonly Regex SearchTokenRegex = new(
        @"[\p{L}]+|[0-9]+(?:\.[0-9]+)?",
        RegexOptions.Compiled);

    private static readonly Regex ReferenceCleanupRegex = new(
        @"[^A-Z0-9]",
        RegexOptions.Compiled);

    public void Search()
    {
        var searchTerm = SearchText?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            UpdateSearchResults(_allArticles);
            return;
        }

        var normalizedReferenceTerm =
            NormalizeReferenceForSearch(searchTerm);

        var scoredResults = new List<(Article Article, double Score)>();

        if (searchTerm.Contains('*'))
        {
            var (prefixText, containsText) =
                SplitSearchPattern(searchTerm);

            var prefixTokens =
                GetSearchTokens(
                    DesignationNormalizer.Normalize(prefixText));

            var containsTokens =
                GetSearchTokens(
                    DesignationNormalizer.Normalize(containsText));

            foreach (var article in _allArticles)
            {
                var score =
                    ScoreSearchMatch(prefixTokens, containsTokens, article);

                if (!string.IsNullOrEmpty(normalizedReferenceTerm))
                {
                    var referenceFournisseur =
                        NormalizeReferenceForSearch(
                            article.ReferenceFournisseur);

                    var referenceFabricant =
                        NormalizeReferenceForSearch(
                            article.ReferenceFabricant);

                    if (referenceFournisseur.Contains(
                            normalizedReferenceTerm,
                            StringComparison.OrdinalIgnoreCase) ||
                        referenceFabricant.Contains(
                            normalizedReferenceTerm,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        score += 25;
                    }
                }

                if (score > 0)
                    scoredResults.Add((article, score));
            }
        }
        else
        {
            var queryTokens =
                GetSearchTokens(
                    DesignationNormalizer.Normalize(searchTerm));

            foreach (var article in _allArticles)
            {
                var score =
                    ScoreSearchMatch(queryTokens, article);

                if (!string.IsNullOrEmpty(normalizedReferenceTerm))
                {
                    var referenceFournisseur =
                        NormalizeReferenceForSearch(
                            article.ReferenceFournisseur);

                    var referenceFabricant =
                        NormalizeReferenceForSearch(
                            article.ReferenceFabricant);

                    if (referenceFournisseur.Contains(
                            normalizedReferenceTerm,
                            StringComparison.OrdinalIgnoreCase) ||
                        referenceFabricant.Contains(
                            normalizedReferenceTerm,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        score += 25;
                    }
                }

                if (score > 0)
                    scoredResults.Add((article, score));
            }
        }

        var results = scoredResults
            .OrderByDescending(x => x.Score)
            .Select(x => x.Article)
            .ToList();

        UpdateSearchResults(results);
    }

    private static (string PrefixText, string ContainsText)
        SplitSearchPattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ("", "");

        var starIndex = value.IndexOf('*');

        if (starIndex < 0)
            return ("", value);

        var prefixText = value[..starIndex].Trim();
        var containsText = value[(starIndex + 1)..].Trim();

        return (prefixText, containsText);
    }

    private static double ScoreSearchMatch(
        string[] prefixTokens,
        string[] containsTokens,
        Article article)
    {
        var designationTokens =
            GetSearchTokens(article.DesignationNormalisee);

        var codeTokens =
            GetSearchTokens(
                DesignationNormalizer.Normalize(article.Code));

        if (!StartsWithTokens(designationTokens, prefixTokens))
            return 0;

        double score = 0;

        if (prefixTokens.Length > 0)
        {
            score += prefixTokens.Length * 20;
        }

        foreach (var queryToken in containsTokens)
        {
            var designationMatches = designationTokens
                .Where(candidate =>
                    TokenMatches(queryToken, candidate))
                .ToList();

            var codeMatches = codeTokens
                .Where(candidate =>
                    TokenMatches(queryToken, candidate))
                .ToList();

            if (designationMatches.Count == 0 &&
                codeMatches.Count == 0)
            {
                return 0;
            }

            if (designationMatches.Any(candidate =>
                    ExactTokenMatch(queryToken, candidate)))
            {
                score += 10;
            }
            else if (codeMatches.Any(candidate =>
                    ExactTokenMatch(queryToken, candidate)))
            {
                score += 8;
            }
            else
            {
                score += 5;
            }
        }

        if (prefixTokens.Length == 0 &&
            containsTokens.Length == 0)
        {
            return 1;
        }

        return score;
    }

    private static bool StartsWithTokens(
        string[] designationTokens,
        string[] prefixTokens)
    {
        if (prefixTokens.Length == 0)
            return true;

        if (designationTokens.Length < prefixTokens.Length)
            return false;

        for (int i = 0; i < prefixTokens.Length; i++)
        {
            if (!TokenMatches(prefixTokens[i], designationTokens[i]))
                return false;
        }

        return true;
    }

    private static string[] GetSearchTokens(string value)
    {
        return SearchTokenRegex
            .Matches(value)
            .Select(m => m.Value)
            .Where(token =>
                !token.Equals("X", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static double ScoreSearchMatch(
        string[] queryTokens,
        Article article)
    {
        if (queryTokens.Length == 0)
            return 1;

        var designationTokens =
            GetSearchTokens(article.DesignationNormalisee);

        var codeTokens =
            GetSearchTokens(
                DesignationNormalizer.Normalize(article.Code));

        double score = 0;

        foreach (var queryToken in queryTokens)
        {
            var designationMatches = designationTokens
                .Where(candidate =>
                    TokenMatches(queryToken, candidate))
                .ToList();

            var codeMatches = codeTokens
                .Where(candidate =>
                    TokenMatches(queryToken, candidate))
                .ToList();

            if (designationMatches.Count == 0 &&
                codeMatches.Count == 0)
            {
                return 0;
            }

            if (designationMatches.Any(candidate =>
                    ExactTokenMatch(queryToken, candidate)))
            {
                score += 10;
            }
            else if (codeMatches.Any(candidate =>
                    ExactTokenMatch(queryToken, candidate)))
            {
                score += 8;
            }
            else
            {
                score += 5;
            }
        }

        return score;
    }

    private static bool TokenMatches(
        string queryToken,
        string candidateToken)
    {
        if (ExactTokenMatch(queryToken, candidateToken))
            return true;

        if (TryParseNumber(queryToken, out var queryNumber) &&
            TryParseNumber(candidateToken, out var candidateNumber))
        {
            if (HasDecimalPart(queryNumber))
                return candidateNumber == queryNumber;

            var candidateIntegerPart =
                decimal.Truncate(candidateNumber);

            return candidateIntegerPart == queryNumber;
        }

        return false;
    }

    private static bool ExactTokenMatch(
        string queryToken,
        string candidateToken)
    {
        if (string.Equals(
            queryToken,
            candidateToken,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryParseNumber(queryToken, out var queryNumber) &&
            TryParseNumber(candidateToken, out var candidateNumber))
        {
            return candidateNumber == queryNumber;
        }

        return false;
    }

    private static bool TryParseNumber(
        string value,
        out decimal number)
    {
        return decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool HasDecimalPart(decimal value)
    {
        return value != decimal.Truncate(value);
    }

    public void SetCompatibilities(
        IEnumerable<Compatibilite> results)
    {
        Compatibilities.Clear();

        foreach (var result in results)
            Compatibilities.Add(result);

        SelectedCompatibility =
            Compatibilities.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name));
    }

    private Article? _selectedArticle;

    public Article? SelectedArticle
    {
        get => _selectedArticle;
        set
        {
            _selectedArticle = value;

            if (_selectedArticle != null)
            {
                _selectedArticle.CheminImage =
                    ArticleImageService.TrouverImage(
                        _selectedArticle.Code);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(ImageSelectionnee));
        }
    }

    public BitmapImage? ImageSelectionnee
    {
        get
        {
            if (SelectedArticle == null)
                return null;

            string? chemin =
                ArticleImageService.TrouverImage(
                    SelectedArticle.Code);

            if (chemin == null)
                return null;

            var image = new BitmapImage();

            image.BeginInit();
            image.UriSource = new Uri(chemin, UriKind.Absolute);
            image.CacheOption =
                BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 300;
            image.EndInit();

            image.Freeze();

            return image;
        }
    }

    public BitmapImage? ImageCompatibiliteSelectionnee
    {
        get
        {
            var article = SelectedCompatibility?.Candidat;

            if (article == null)
                return null;

            string? chemin =
                ArticleImageService.TrouverImage(
                    article.Code);

            if (chemin == null)
                return null;

            var image = new BitmapImage();

            image.BeginInit();
            image.UriSource = new Uri(chemin, UriKind.Absolute);
            image.CacheOption =
                BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 220;
            image.EndInit();

            image.Freeze();

            return image;
        }
    }

    public CompatibilityWeights Weights { get; } = new();

    private static string NormalizeReferenceForSearch(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized =
            DesignationNormalizer.Normalize(value);

        return ReferenceCleanupRegex.Replace(
            normalized,
            "");
    }
}