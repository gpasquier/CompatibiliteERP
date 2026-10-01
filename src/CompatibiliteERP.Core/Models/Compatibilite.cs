using System.Collections.Generic;
using CompatibiliteERP.Core.Models;

namespace CompatibiliteERP.Core.Models;

public sealed class Compatibilite
{
    public required Article Candidat { get; init; } // Utilisez 'init' pour rendre la propriété immuable
    public double Score { get; init; }
    public NiveauCompatibilite Niveau { get; init; }
    public List<ComparaisonCritere> Criteres { get; init; } = new(); // Initialisation correcte de la liste
    public List<string> Alertes { get; init; } = new(); // Initialisation correcte de la liste

    // Propriété pour accéder à la désignation du candidat
    public string CandidatDesignation => Candidat.Designation; 
}