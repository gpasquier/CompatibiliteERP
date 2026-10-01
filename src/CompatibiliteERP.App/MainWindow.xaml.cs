using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CompatibiliteERP.App.ViewModels;
using CompatibiliteERP.Core.Services;
using CompatibiliteERP.Core.Models;
using CompatibiliteERP.App.Data;

namespace CompatibiliteERP.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly CompatibilityEngine _engine = new();
    private readonly SqlArticleRepository _repository;

    private GridViewColumnHeader? _lastSortColumn;
    private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;

    private readonly DesignationParser _parser; // Déclaration de _parser

    public MainWindow()
    {
        InitializeComponent();

        _repository = new SqlArticleRepository(SqlConfiguration.ConnectionString);
        _parser = new DesignationParser(); // Initialisation de _parser

        DataContext = _vm;

        Loaded += async (_, _) =>
        {
            await _vm.LoadArticlesAsync();
        };

        Loaded += MainWindow_Loaded;

        _vm.Weights.PropertyChanged += (_, _) =>
        {
            AnalyzeSelected();
        };

        CompatibilitiesList.AddHandler(
            GridViewColumnHeader.ClickEvent,
            new RoutedEventHandler(CompatibilitiesList_HeaderClick));
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            AnalyzeSelected();
        }
        catch (Exception ex)
        {
            _vm.Status = $"Erreur de chargement : {ex.Message}";
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e) => _vm.Search();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _vm.Search();
    }

    private void Analyze_Click(object sender, RoutedEventArgs e) => AnalyzeSelected();

    private void ResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
            AnalyzeSelected();
    }

    private void AnalyzeSelected()
    {
        if (_vm.SelectedArticle is null)
            return;

        var sw = Stopwatch.StartNew();

        // Mettez à jour les poids directement via les propriétés
        _vm.Weights.WeightReferenceFournisseur = string.IsNullOrWhiteSpace(_vm.SelectedArticle.ReferenceFournisseur) ? 0 : CompatibilityWeights._weightReferenceFournisseurStatic;
        _vm.Weights.WeightReferenceFabricant = string.IsNullOrWhiteSpace(_vm.SelectedArticle.ReferenceFabricant) ? 0 : CompatibilityWeights._weightReferenceFabricantStatic;
        _vm.Weights.WeightDim1 = string.IsNullOrWhiteSpace(_vm.SelectedArticle.Caracteristiques?.Dimension1.ToString()) ? 0 : CompatibilityWeights._weightDim1Static;
        _vm.Weights.WeightDim2 = string.IsNullOrWhiteSpace(_vm.SelectedArticle.Caracteristiques?.Dimension2.ToString()) ? 0 : CompatibilityWeights._weightDim2Static;
        _vm.Weights.WeightDim3 = string.IsNullOrWhiteSpace(_vm.SelectedArticle.Caracteristiques?.Dimension3.ToString()) ? 0 : CompatibilityWeights._weightDim3Static;
        _vm.Weights.WeightDim4 = string.IsNullOrWhiteSpace(_vm.SelectedArticle.Caracteristiques?.Dimension4.ToString()) ? 0 : CompatibilityWeights._weightDim4Static;
        _vm.Weights.WeightMatiere = string.IsNullOrWhiteSpace(_vm.SelectedArticle.Caracteristiques?.Matiere) ? 0 : CompatibilityWeights._weightMatiereStatic;
        _vm.Weights.WeightNorme = string.IsNullOrWhiteSpace(_vm.SelectedArticle.Caracteristiques?.Norme) ? 0 : CompatibilityWeights._weightNormeStatic;
        
        var results = _engine.FindCandidates(
            _vm.SelectedArticle,
            _vm.Articles,
            _vm.Weights,
            12);

        _vm.SetCompatibilities(results);

        sw.Stop();

        _vm.Status =
            $"Analyse terminée — {results.Count} candidat(s) — " +
            $"{sw.ElapsedMilliseconds} ms — base {_vm.Articles.Count} références.";
    }

    private void CompatibilitiesList_HeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header)
            return;

        if (header.Tag is not string sortBy || string.IsNullOrWhiteSpace(sortBy))
        {
            return;
        }

        var direction = _lastSortColumn == header && _lastSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        var view = CollectionViewSource.GetDefaultView(CompatibilitiesList.ItemsSource);

        if (view == null)
            return;

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(sortBy, direction));
        view.Refresh();

        _lastSortColumn = header;
        _lastSortDirection = direction;
    }

    public async Task LoadArticlesAsync()
    {
        try
        {
            LoadingProgressBar.Visibility = Visibility.Visible; // Afficher le ProgressBar

            var articles = await _repository.LoadArticlesAsync(); // Assurez-vous que cette méthode renvoie une liste d'articles

            for (int i = 0; i < articles.Count; i++)
            {
                var article = articles[i];

                if (article.Id == 0)
                    article.Id = i + 1;

                var (caracteristiques, confidence) = _parser.Extract(article.Designation); // Assurez-vous que le parser fonctionne correctement

                article.Famille = article.Famille.Trim(); // Vérifiez que Famille est bien une propriété de l'article
                article.Caracteristiques = caracteristiques; // Assurez-vous que Caracteristiques est bien une propriété de l'article
                article.ConfianceExtraction = confidence; // Vérifiez que ConfianceExtraction est bien une propriété de l'article
                article.DesignationNormalisee = DesignationNormalizer.Normalize(article.Designation); // Vérifiez que cette méthode fonctionne
            }

            _vm.SetArticles(articles); // Assurez-vous que cette méthode est définie et fonctionne correctement

            _vm.Status = $"{articles.Count} référence(s) chargée(s) depuis SQL Server."; // Utilisez articles.Count pour le nombre d'articles chargés
        }
        catch (Exception ex)
        {
            _vm.Status = $"Erreur SQL : {ex.Message}"; // Affichez l'erreur si une exception est levée
        }
        finally
        {
            LoadingProgressBar.Visibility = Visibility.Collapsed; // Masquer le ProgressBar
        }
    }

}
