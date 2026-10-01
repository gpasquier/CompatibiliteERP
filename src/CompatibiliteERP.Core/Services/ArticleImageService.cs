namespace CompatibiliteERP.App.Services;

public static class ArticleImageService
{
    private const string RacineImages = @"\\fsclip9\THIBAUT\ARTICLES\ARTICLES";

    public static string? TrouverImage(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        string dossier = Path.Combine(RacineImages, reference);

        string jpg = Path.Combine(dossier, reference + ".jpg");

        if (File.Exists(jpg))
            return jpg;

        string jpeg = Path.Combine(dossier, reference + ".jpeg");

        if (File.Exists(jpeg))
            return jpeg;

        return null;
    }
}