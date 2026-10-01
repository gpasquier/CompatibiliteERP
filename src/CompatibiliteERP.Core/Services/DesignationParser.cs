using System.Globalization;
using System.Text.RegularExpressions;
using CompatibiliteERP.Core.Models;

namespace CompatibiliteERP.Core.Services;

public sealed class DesignationParser
{
    private static readonly Regex ThreeDimensions =
        new(@"(?<!\d)(\d+(?:[.,]\d+)?)X(\d+(?:[.,]\d+)?)X(\d+(?:[.,]\d+)?)(?!\d)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TwoDimensions =
        new(@"(?<!\d)(\d+(?:[.,]\d+)?)X(\d+(?:[.,]\d+)?)(?!\d)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Nominal =
        new(@"\b(?:M|Z|L|LL)\s*0*(\d+(?:[.,]\d+)?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Standard =
        new(@"\b(DIN\s*\d{2,5}|ISO\s*\d{2,5}|NF\s*[AE]?\s*\d{1,3}(?:-\d{1,3})?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public (
        Caracteristiques Caracteristiques,
        double ConfianceExtraction)
        Extract(string designation)
    {
        var normalizedDesignation =
            DesignationNormalizer.Normalize(designation);

        var chars = new Caracteristiques();

        var dim = ThreeDimensions.Match(normalizedDesignation);
        if (dim.Success)
        {
            chars.Dimension2 = Number(dim.Groups[1].Value);
            chars.Dimension3 = Number(dim.Groups[2].Value);
            chars.Dimension4 = Number(dim.Groups[3].Value);
        }
        else
        {
            var two = TwoDimensions.Match(normalizedDesignation);
            if (two.Success)
            {
                chars.Dimension2 = Number(two.Groups[1].Value);
                chars.Dimension3 = Number(two.Groups[2].Value);
            }
        }

        // Nouvelle logique pour extraire les dimensions supplémentaires
        var additionalDimensions = ExtractAdditionalDimensions(normalizedDesignation);
        if (additionalDimensions.Count > 0)
        {
            chars.Dimension1 = additionalDimensions[0]; // M17
            if (additionalDimensions.Count > 1) chars.Dimension2 = additionalDimensions[1]; // 17
            if (additionalDimensions.Count > 2) chars.Dimension3 = additionalDimensions[2]; // 19
        }

        var nominal = Nominal.Match(normalizedDesignation);
        if (nominal.Success)
            chars.Dimension1 = Number(nominal.Groups[1].Value);

        chars.Norme = Standard.Match(normalizedDesignation).Value.NullIfEmpty();
        chars.Matiere = DetectMaterial(normalizedDesignation);
        chars.Traitement = DetectTreatment(normalizedDesignation);
        chars.Fonction = DetectFunction(normalizedDesignation);

        var confidence = ComputeConfidence(chars);

        return (chars, confidence);
    }

    private List<double> ExtractAdditionalDimensions(string designation)
    {
        var dimensions = new List<double>();
        var regex = new Regex(@"(\d+)\s*(?=M|PTS|MM²)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var matches = regex.Matches(designation);

        foreach (Match match in matches)
        {
            if (double.TryParse(match.Value, out double dimension))
            {
                dimensions.Add(dimension);
            }
        }

        return dimensions;
    }

    private static string? DetectMaterial(string s)
    {
        if (s.Contains("INOX A2") || s.Contains("INOX A4") || s.Contains("INOX 304L") || s.Contains("INOX")) return "INOX";
        if (s.Contains("LAITON")) return "LAITON";
        if (s.Contains("POLYURETHANE")) return "POLYURETHANE";
        if (s.Contains("ELASTOMERE")) return "ELASTOMERE";
        if (s.Contains("ACIER")) return "ACIER";
        if (s.Contains("ACZI")) return "ACIER";
        if (s.Contains("BRONZE")) return "BRONZE";
        if (s.Contains("PA 6.6") || s.Contains("PEHD") || s.Contains("TECHNYL") || s.Contains("NYLON")) return "POLYMERE";
        return null;
    }

    private static string? DetectTreatment(string s)
    {
        if (s.Contains("BICHROMATE") || s.Contains("BICHROMATAGE")) return "ZINGUE";
        if (s.Contains("ZINGUE") || s.Contains("ZINGUEE") || s.Contains("ZINGAGE") || s.Contains("ACZI") || s.Contains("ELECTROZINGUE")) return "ZINGUE";
        if (s.Contains("BRUT")) return "BRUT";
        return null;
    }

    private static string? DetectFunction(string s)
    {
        string[] functions =
        [
            "ETANCHEITE", "CENTRAGE", "CALAGE", "SERRAGE", "BLOCAGE",
            "MAINTIEN", "APPUI", "TENSION", "OBTURATION", "DETECTION",
            "NIVELAGE", "ROTATION", "PIGNON", "ROULEMENT"
        ];

        return functions.FirstOrDefault(s.Contains);
    }

    private static double ComputeConfidence(Caracteristiques c)
    {
        var score = 0.35;

        if (c.Dimension2.HasValue) score += 0.2;
        if (c.Dimension3.HasValue) score += 0.2;
        if (c.Dimension4.HasValue) score += 0.1;
        if (c.Norme is not null) score += 0.1;

        return Math.Min(1.0, score);
    }

    private static double Number(string value) =>
        double.Parse(
            value.Replace(',', '.'),
            CultureInfo.InvariantCulture);

    public double CompareReferences(string referenceAnalyzed, string referenceSupplier, string referenceManufacturer)
    {
        var cleanedAnalyzed = CleanReference(referenceAnalyzed);
        var cleanedSupplier = CleanReference(referenceSupplier);
        var cleanedManufacturer = CleanReference(referenceManufacturer);

        double similarityScore = 0.0;

        // Calculer la similarité pour le fournisseur
        if (IsSimilar(cleanedAnalyzed, cleanedSupplier))
        {
            similarityScore += 0.5; // Poids fort pour le fournisseur
        }

        // Calculer la similarité pour le fabricant
        if (IsSimilar(cleanedAnalyzed, cleanedManufacturer))
        {
            similarityScore += 0.5; // Poids fort pour le fabricant
        }

        return similarityScore;
    }

    private string CleanReference(string reference)
    {
        // Enlever les caractères spéciaux et les espaces
        return Regex.Replace(reference, @"[^a-zA-Z0-9]", "").Trim();
    }

    private bool IsSimilar(string reference1, string reference2)
    {
        // Définir un seuil de similarité, par exemple, 80% de correspondance
        return reference1.Length > 0 && reference2.Length > 0 && 
               reference1.Equals(reference2, StringComparison.OrdinalIgnoreCase);
    }

    public bool ValidateReferences(string designation, string referenceSupplier, string referenceManufacturer)
    {
        var cleanedDesignation = CleanReference(designation);
        var cleanedSupplier = CleanReference(referenceSupplier);
        var cleanedManufacturer = CleanReference(referenceManufacturer);

        return IsSimilar(cleanedDesignation, cleanedSupplier) || IsSimilar(cleanedDesignation, cleanedManufacturer);
    }

    public bool SearchSimilarTerms(string searchTerm, string designation, string referenceSupplier, string referenceManufacturer)
    {
        var cleanedSearchTerm = CleanReference(searchTerm);
        var cleanedDesignation = CleanReference(designation);
        var cleanedSupplier = CleanReference(referenceSupplier);
        var cleanedManufacturer = CleanReference(referenceManufacturer);

        if (cleanedSearchTerm.Length == 0)
            return true;

        return cleanedDesignation.Contains(cleanedSearchTerm, StringComparison.OrdinalIgnoreCase) ||
               cleanedSupplier.Contains(cleanedSearchTerm, StringComparison.OrdinalIgnoreCase) ||
               cleanedManufacturer.Contains(cleanedSearchTerm, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
