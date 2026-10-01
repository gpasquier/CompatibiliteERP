using System.Globalization;
using System.Text;

namespace CompatibiliteERP.Core.Services;

public static class DesignationNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = text.ToUpperInvariant();

        // Normalisation Unicode
        text = text.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();

        foreach (char c in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            // Suppression des accents
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c);
        }

        text = sb.ToString().Normalize(NormalizationForm.FormC);

        // Variantes de multiplication
        text = text
            .Replace("×", "X")
            .Replace("✕", "X")
            .Replace("*", "X");

        // Diamètre
        text = text.Replace("Ø", "D");

        // Virgule décimale
        text = text.Replace(',', '.');

        // Espaces multiples
        text = string.Join(
            " ",
            text.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries));

        return text.Trim();
    }
}