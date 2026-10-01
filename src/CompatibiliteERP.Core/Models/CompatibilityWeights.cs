using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace CompatibiliteERP.Core.Models;

public sealed class CompatibilityWeights : INotifyPropertyChanged
{

    public static double _weightFamilleStatic = 25;
    public static double _weightReferenceFournisseurStatic = 10;
    public static double _weightReferenceFabricantStatic = 10;
    public static double _weightDim1Static = 20;
    public static double _weightDim2Static = 20;
    public static double _weightDim3Static = 10;
    public static double _weightDim4Static = 1;
    public static double _weightMatiereStatic = 1;
    public static double _weightNormeStatic = 1;

    public double _weightFamille = _weightFamilleStatic;
    public double _weightReferenceFournisseur = _weightReferenceFournisseurStatic;
    public  double _weightReferenceFabricant = 10;
    public double _weightDim1 = _weightDim1Static;
    public double _weightDim2 = _weightDim2Static;
    public double _weightDim3 = _weightDim3Static;
    public double _weightDim4 = _weightDim4Static;
    public double _weightMatiere = _weightMatiereStatic;
    public double _weightNorme = _weightNormeStatic;

    public double WeightFamille
    {
        get => _weightFamille;
        set
        {
            if (_weightFamille == value)
                return;

            _weightFamille = value;
            OnPropertyChanged();
        }
    }

    public double WeightReferenceFournisseur
    {
        get => _weightReferenceFournisseur;
        set
        {
            if (_weightReferenceFournisseur == value)
                return;

            _weightReferenceFournisseur = value;
            OnPropertyChanged();
        }
    }

    public double WeightReferenceFabricant
    {
        get => _weightReferenceFabricant;
        set
        {
            if (_weightReferenceFabricant == value)
                return;

            _weightReferenceFabricant = value;
            OnPropertyChanged();
        }
    }

    public double WeightDim1
    {
        get => _weightDim1;
        set
        {
            if (_weightDim1 == value)
                return;

            _weightDim1 = value;
            OnPropertyChanged();
        }
    }
    public double WeightDim2
    {
        get => _weightDim2;
        set
        {
            if (_weightDim2 == value)
                return;

            _weightDim2 = value;
            OnPropertyChanged();
        }
    }

    public double WeightDim3
    {
        get => _weightDim3;
        set
        {
            if (_weightDim3 == value)
                return;

            _weightDim3 = value;
            OnPropertyChanged();
        }
    }

    public double WeightDim4
    {
        get => _weightDim4;
        set
        {
            if (_weightDim4 == value)
                return;

            _weightDim4 = value;
            OnPropertyChanged();
        }
    }

    public double WeightMatiere
    {
        get => _weightMatiere;
        set
        {
            if (_weightMatiere == value)
                return;

            _weightMatiere = value;
            OnPropertyChanged();
        }
    }

    public double WeightNorme
    {
        get => _weightNorme;
        set
        {
            if (_weightNorme == value)
                return;

            _weightNorme = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}