# CompatibiliteERP

Prototype C# / .NET 8 / WPF pour analyser des désignations ERP et proposer des références potentiellement compatibles.

## Choix techniques

- **.NET 8**
- **WPF** pour l'application desktop Windows
- Projet `CompatibiliteERP.Core` indépendant de l'interface
- Extraction déterministe par expressions régulières
- Moteur de compatibilité explicable
- Aucune dépendance NuGet externe

WPF est une technologie desktop Windows de .NET et prend en charge XAML, data binding et personnalisation de l'interface. Voir la documentation Microsoft Learn : https://learn.microsoft.com/dotnet/desktop/wpf/

## Structure

```text
CompatibiliteERP/
├── CompatibiliteERP.sln
├── README.md
├── data/
│   └── sample_designations.csv
└── src/
    ├── CompatibiliteERP.Core/
    │   ├── Models/
    │   └── Services/
    └── CompatibiliteERP.App/
        ├── App.xaml
        ├── MainWindow.xaml
        └── ViewModels/
```

## Lancer

Prérequis :

- Windows
- Visual Studio 2022 avec le workload .NET Desktop Development
- SDK .NET 8

Depuis le dossier du projet :

```bash
dotnet restore
dotnet build
dotnet run --project src/CompatibiliteERP.App/CompatibiliteERP.App.csproj
```

## Ce que fait le prototype

1. Charge les désignations de `data/sample_designations.csv`.
2. Normalise le texte : casse, accents, espaces, `×`/`x`, virgules décimales.
3. Extrait :
   - diamètre nominal,
   - diamètre intérieur,
   - diamètre extérieur,
   - épaisseur,
   - matière,
   - traitement,
   - norme,
   - fonction.
4. Classe la référence dans une sous-famille.
5. Compare les références selon des critères techniques.
6. Retourne un niveau :
   - `Compatible`
   - `SousConditions`
   - `Incompatible`
7. Affiche les critères et alertes expliquant le résultat.

## Limites volontairement conservées

Le prototype ne prétend pas déterminer à lui seul l'équivalence mécanique réelle.

Une désignation peut être incomplète, ambiguë ou spécifique à une machine. La compatibilité doit donc être validée par les règles métier et, lorsque nécessaire, par un référent technique.

Le score est un outil de présélection, pas une certification d'interchangeabilité.

## Prochaine étape recommandée

Pour passer de ce prototype à la base ERP d'environ 20 000 références :

1. Remplacer le CSV par SQL Server.
2. Ajouter une table de synonymes / abréviations.
3. Ajouter des règles par sous-famille.
4. Ajouter les tolérances par critère.
5. Ajouter un mode `strict` et un mode `souple`.
6. Ajouter la validation utilisateur (`compatible`, `incompatible`, `à vérifier`).
7. Ajouter ensuite une recherche sémantique/embeddings pour réduire le nombre de candidats avant le moteur de règles.

Les embeddings ne doivent pas décider seuls de la compatibilité : ils servent à retrouver les candidats, tandis que les règles techniques expliquent et décident le résultat.
