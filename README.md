# RencontresTables — .NET 8, Google OR-Tools CP-SAT et placement complémentaire

Application console C# qui place **tous les participants** dans des tables virtuelles, en distinguant les placements stricts de CP-SAT des placements complémentaires.

## Prérequis

- SDK **.NET 8** (x64 recommandé), Visual Studio 2022 facultatif.
- Accès NuGet pour restaurer `Google.OrTools 9.15.6755` et `Newtonsoft.Json 13.0.3`.

## Démarrage

Ouvrir le dossier du projet, puis exécuter :

```bash
dotnet restore
dotnet run -- hommes.json femmes.json 15
```

Sur Windows, vous pouvez également lancer `Lancer_Windows.bat`.

Le dernier argument est le **temps maximal accordé au solveur en secondes** (par défaut 15, de 0,1 à 600 secondes). Ce n'est pas le nombre d'essais de l'ancienne version.

Exemple de fichiers JSON (présents dans l'archive) :

```json
[
  {
    "Prenom": "Kevin",
    "Nom": "Smith",
    "Age": 32,
    "TranchesRecherchees": ["25-30", "30-35"]
  }
]
```

## Phases de répartition

**Phase 1 — Google OR-Tools CP-SAT :** recherche de tables composées de 5 hommes + 5 femmes, puis 4+4, 3+3, 2+2. Chaque homme d'une table doit être réciproquement compatible avec toutes les femmes de cette table. Une personne ne peut apparaître qu'une fois. Le solveur maximise d'abord le nombre placé, puis favorise les grandes tables. Le statut `Optimal` prouve l'optimalité *de cette phase stricte* ; `Feasible` indique une solution valide sans preuve d'optimalité.

**Phase 2 — Placement par intervalle :** chaque personne non retenue par CP-SAT rejoint, si possible, une table existante où **son âge est compris entre l'âge minimum et maximum des personnes du sexe opposé déjà présentes**. On privilégie le plus grand nombre de compatibilités réciproques puis l'équilibre de la table.

**Phase 3 — Placement par proximité :** parmi les personnes encore disponibles, chacune est affectée si possible à une table existante, en minimisant la distance de son âge à l'intervalle d'âge du sexe opposé présent.

**Phase 4 — Nouvelles tables :** si les tables précédentes n'ont plus de capacité adéquate, on répartit tous les participants restants dans de nouvelles tables, de manière aussi équilibrée que possible entre hommes et femmes. Leur répartition tient compte de leurs âges. Si les effectifs le rendent inévitable, une table complémentaire peut n'avoir qu'un seul sexe.

**Capacité : maximum 5 hommes et 5 femmes par table**, y compris les tables complémentaires. Les phases 2 à 4 ne garantissent **ni la parité, ni la compatibilité réciproque** : c'est un choix assumé pour que tous les inscrits aient une table. Le programme distingue ces ajouts pour éviter de les confondre avec le résultat CP-SAT strict.

## Affichage

La liste séparée des personnes non placées est supprimée : tout le monde apparaît dans les tables.

- **Rose / magenta** : femmes retenues lors de la phase CP-SAT.
- **Bleu / cyan** : hommes retenus lors de la phase CP-SAT.
- **Jaune** : femmes **et** hommes ajoutés après la phase CP-SAT, y compris dans les nouvelles tables.

Chaque ligne affiche **nom complet + âge + toutes les tranches recherchées**, dans **une couleur unique** :

```text
Femmes :
Sarah Einstein 30 [25-30][30-35]
Hommes :
Kevin Smith 32 [25-30][30-35]
```

La console fournit aussi les nombres de personnes placées par CP-SAT, par intervalle, par proximité et dans les nouvelles tables, ainsi que le statut et la durée du solveur. La section « personnes non placées » n'existe plus.

## Fichiers

- `Program.cs` : chargement des fichiers avec Newtonsoft.Json, affichage coloré et lancement des phases.
- `Models.cs` : profils, tables et métriques.
- `RepartiteurCpSat.cs` : optimisation mathématique, inchangée sur ses contraintes strictes.
- `PlacementComplementaire.cs` : les trois phases d'affectation après CP-SAT ; marque les nouveaux membres pour l'affichage en jaune.
- `hommes.json`, `femmes.json` : jeu de démonstration de 10 hommes et 10 femmes.
- `RencontresTables.csproj` : projet .NET 8.

## Important

Le statut `Optimal` signifie que **la répartition stricte CP-SAT** a atteint son optimum mathématique, pas que les placements complémentaires sont des rencontres mutuellement compatibles. Les phases complémentaires peuvent créer une table déséquilibrée ou composée de personnes non compatibles : ce choix vise à n'exclure personne et doit être validé selon les règles métier de votre application.

Le SDK .NET n'est pas disponible dans l'environnement de génération : la compilation et l'exécution restent à vérifier sur un poste doté de .NET 8.
