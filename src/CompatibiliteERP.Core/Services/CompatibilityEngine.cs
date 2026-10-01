using System;
using System.Collections.Generic;
using System.Linq;
using CompatibiliteERP.Core.Models;

namespace CompatibiliteERP.Core.Services;

/// <summary>
/// Moteur simplifié : compare textes et valeurs numériques
/// avec pondération paramétrable.
/// </summary>
public sealed class CompatibilityEngine
{
    public IReadOnlyList<Compatibilite> FindCandidates(
        Article source,
        IReadOnlyList<Article> allArticles,
        CompatibilityWeights? weights = null,
        int limit = 12)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(allArticles);

        if (limit <= 0)
            return Array.Empty<Compatibilite>();

        weights ??= new CompatibilityWeights();

        return allArticles
            .Where(article => article.Id != source.Id)
            .Select(article => Compare(source, article, weights))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Candidat.Designation)
            .Take(limit)
            .ToList();
    }

    public Compatibilite Compare(
        Article source,
        Article candidate,
        CompatibilityWeights? weights = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);

        weights ??= new CompatibilityWeights();

        var criteria = new List<ComparaisonCritere>();
        var alerts = new List<string>();
        var weighted = new List<(double Points, double PointsPossibles)>();

        // Ajoutez la comparaison de désignation
        AddTextCriterion(
            "Désignation",
            source.Designation,
            candidate.Designation,
            5.0, // Poids pour la désignation
            criteria,
            weighted,
            alerts);

        AddTextCriterion(
            "Famille",
            source.Famille,
            candidate.Famille,
            weights.WeightFamille,
            criteria,
            weighted,
            alerts);

        AddTextCriterion(
            "Référence fournisseur",
            source.ReferenceFournisseur,
            candidate.ReferenceFournisseur,
            weights.WeightReferenceFournisseur,
            criteria,
            weighted,
            alerts);

        AddTextCriterion(
            "Référence fabricant",
            source.ReferenceFabricant,
            candidate.ReferenceFabricant,
            weights.WeightReferenceFabricant,
            criteria,
            weighted,
            alerts);

        AddNumericCriterion(
            "Dimension 1",
            source.Caracteristiques?.Dimension1,
            candidate.Caracteristiques?.Dimension1,
            0.05,
            weights.WeightDim1,
            criteria,
            weighted,
            alerts);

        AddNumericCriterion(
            "Dimension 2",
            source.Caracteristiques?.Dimension2,
            candidate.Caracteristiques?.Dimension2,
            0.05,
            weights.WeightDim2,
            criteria,
            weighted,
            alerts);

        AddNumericCriterion(
            "Dimension 3",
            source.Caracteristiques?.Dimension3,
            candidate.Caracteristiques?.Dimension3,
            0.05,
            weights.WeightDim3,
            criteria,
            weighted,
            alerts);

        AddNumericCriterion(
            "Dimension 4",
            source.Caracteristiques?.Dimension4,
            candidate.Caracteristiques?.Dimension4,
            0.10,
            weights.WeightDim4,
            criteria,
            weighted,
            alerts);

        AddTextCriterion(
            "Matière",
            source.Caracteristiques?.Matiere,
            candidate.Caracteristiques?.Matiere,
            weights.WeightMatiere,
            criteria,
            weighted,
            alerts);

        AddTextCriterion(
            "Norme",
            source.Caracteristiques?.Norme,
            candidate.Caracteristiques?.Norme,
            weights.WeightNorme,
            criteria,
            weighted,
            alerts);

        var totalPossible = weighted.Sum(x => x.PointsPossibles);
        var totalObtained = weighted.Sum(x => x.Points);

        var score = totalPossible <= 0
            ? 0
            : Math.Clamp(totalObtained / totalPossible * 100, 0, 100);

        var niveau = score switch
        {
            >= 80 => NiveauCompatibilite.Compatible,
            >= 50 => NiveauCompatibilite.Sous_conditions,
            _ => NiveauCompatibilite.Incompatible
        };

        return new Compatibilite
        {
            Candidat = candidate,
            Score = Math.Round(score, 1),
            Niveau = niveau,
            Criteres = criteria,
            Alertes = alerts
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public double CalculateCompatibilityScore(string referenceDesignation, string candidateDesignation)
    {
        // Normaliser les désignations
        var normalizedReference = NormalizeDesignation(referenceDesignation);
        var normalizedCandidate = NormalizeDesignation(candidateDesignation);

        // Comparer les désignations
        if (normalizedReference.Equals(normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return 100; // Score parfait
        }

        // Exemple de calcul de score basé sur la présence de mots-clés
        var referenceWords = normalizedReference.Split(' ');
        var candidateWords = normalizedCandidate.Split(' ');

        int matchCount = referenceWords.Count(word => candidateWords.Contains(word, StringComparer.OrdinalIgnoreCase));
        double score = (double)matchCount / referenceWords.Length * 100;

        // Appliquer des ajustements basés sur la distance de Levenshtein
        double levenshteinScore = CalculateLevenshteinScore(referenceDesignation, candidateDesignation);
        score = Math.Max(score, levenshteinScore);

        return score;
    }

    private string NormalizeDesignation(string designation)
    {
        // Normaliser la désignation (par exemple, supprimer les espaces, mettre en minuscules, etc.)
        return designation.Trim().ToLower();
    }

    private static void AddTextCriterion(
        string name,
        string? source,
        string? candidate,
        double weight,
        List<ComparaisonCritere> criteria,
        List<(double Points, double PointsPossibles)> weighted,
        List<string> alerts)
    {
        if (weight <= 0)
            return;

        var (points, result) = CompareTextSimple(source, candidate, weight);

        
        criteria.Add(new ComparaisonCritere
        {
            Nom = name,
            Resultat = $"{result}",
            Points = points,
            PointsPossibles = weight,
            Bloquant = false
        });

        weighted.Add((points, weight));

        if (points < weight && !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(candidate))
        {
            alerts.Add($"{name} différent(e).");
        }
    }

    private static (double Points, string Resultat) CompareTextSimple(
        string? source,
        string? candidate,
        double weight)
    {
        if (string.IsNullOrWhiteSpace(source) ||
            string.IsNullOrWhiteSpace(candidate))
        {
            return (0, "Non renseigné");
        }

        if (NormalizeExact(source) == NormalizeExact(candidate))
            return (weight, "Identique");

        var sourceWords = TokenizeWords(source);
        var candidateWords = TokenizeWords(candidate);

        if (sourceWords.Count == 0 || candidateWords.Count == 0)
            return (0, $"{source} → {candidate}");

        var common = sourceWords
            .Intersect(candidateWords)
            .Count();

        var union = sourceWords
            .Union(candidateWords)
            .Count();

        var similarity = union == 0
            ? 0
            : (double)common / union;

        var points = Math.Round(weight * similarity, 3);

        return (points, $"{source} → {candidate}");
    }

    private static void AddNumericCriterion(
        string name,
        double? source,
        double? candidate,
        double tolerance,
        double weight,
        List<ComparaisonCritere> criteria,
        List<(double Points, double PointsPossibles)> weighted,
        List<string> alerts)
    {
        if (weight <= 0)
            return;

        if (!source.HasValue || !candidate.HasValue)
        {
            criteria.Add(new ComparaisonCritere
            {
                Nom = name,
                Resultat = "Non renseigné",
                Points = 0,
                PointsPossibles = weight,
                Bloquant = false
            });

            weighted.Add((0, weight));
            return;
        }

        var relativeDiff = GetRelativeDifference(source.Value, candidate.Value);

        if (relativeDiff <= tolerance)
        {
            criteria.Add(new ComparaisonCritere
            {
                Nom = name,
                Resultat = $"Compatible", // Affiche le score maximal
                Points = weight,
                PointsPossibles = weight,
                Bloquant = false
            });

            weighted.Add((weight, weight));
            return;
        }

        var maxRelativeDiff = tolerance * 3;
        var similarity = relativeDiff >= maxRelativeDiff
            ? 0
            : 1 - ((relativeDiff - tolerance) / (maxRelativeDiff - tolerance));

        var points = Math.Round(Math.Max(0, similarity) * weight, 3);

        // Modifiez le résultat pour inclure le score
        criteria.Add(new ComparaisonCritere
        {
            Nom = name,
            Resultat = $"{source} → {candidate}", // Affiche le score obtenu par rapport au score maximal
            Points = points,
            PointsPossibles = weight,
            Bloquant = false
        });

        weighted.Add((points, weight));

        if (points < weight)
        {
            alerts.Add($"{name} différent : {source:0.###} → {candidate:0.###}");
        }
    }

    private static double GetRelativeDifference(
        double a,
        double b)
    {
        if (a == b)
            return 0;

        var diff = Math.Abs(a - b);

        return Math.Abs(a) > 0
            ? diff / Math.Abs(a)
            : diff;
    }

    private static string NormalizeExact(string value)
    {
        return value.Trim()
            .ToUpperInvariant()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);
    }

    private static HashSet<string> TokenizeWords(string value)
    {
        return value
            .ToUpperInvariant()
            .Split(
                [' ', '-', '_', '/', '.', ','],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private double CalculateLevenshteinScore(string source, string target)
    {
        int maxLen = Math.Max(source.Length, target.Length);
        if (maxLen == 0) return 100; // Both strings are empty

        // Calculate Levenshtein distance
        double distance = ComputeLevenshteinDistance(source, target);

        // Normalize to get a score between 0 and 100
        double score = (1 - distance / maxLen) * 100;
        return score;
    }

    private int ComputeLevenshteinDistance(string source, string target)
    {
        int n = source.Length;
        int m = target.Length;
        var dp = new int[n + 1, m + 1];

        // Initialize the distance matrix
        for (int i = 0; i <= n; i++) dp[i, 0] = i;
        for (int j = 0; j <= m; j++) dp[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[n, m];
    }
}

