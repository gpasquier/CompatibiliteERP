namespace CompatibiliteERP.Core.Models;

public class Article
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Designation { get; set; } = "";

    public string DesignationNormalisee { get; set; } = "";

    public string Famille { get; set; } = "";

    public string ReferenceFournisseur { get; set; } = "";

    public string ReferenceFabricant { get; set; } = "";

    public Caracteristiques Caracteristiques { get; set; } = new();

    public double ConfianceExtraction { get; set; }

    public string? CheminImage { get; set; }
}

public class Caracteristiques
{
    public double? Dimension1 { get; set; }
    public double? Dimension2 { get; set; }
    public double? Dimension3 { get; set; }
    public double? Dimension4 { get; set; }
    public string? Matiere { get; set; }
    public string? Nuance { get; set; }
    public string? Traitement { get; set; }
    public string? Norme { get; set; }
    public string? Fonction { get; set; }
    public Dictionary<string, string> Autres { get; set; } = new();
}

public enum NiveauCompatibilite
{
    Incompatible,
    Sous_conditions,
    Compatible
}

public sealed class ComparaisonCritere
{
    public required string Nom { get; init; }
    public required string Resultat { get; init; }
    public double Points { get; init; }
    public double PointsPossibles { get; init; }
    public bool Bloquant { get; init; }
}

public enum TypeComparaison
{
    Egalite,
    Proximite,
    Presence
}
