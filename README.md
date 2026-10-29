# RencontresTables — .NET 8 / Google OR-Tools CP-SAT

Application console répartissant des participants dans des tables virtuelles. Cette version conserve le solveur original et ajoute deux phases de rattrapage dans `Program.cs` via `PlacementComplementaire`.

## Installation

Prérequis : SDK .NET 8 et accès NuGet. Dans le dossier du projet :

```bash
dotnet restore
dotnet run -- hommes.json femmes.json 15
```

Le dernier argument est le délai du solveur CP-SAT en secondes (0,1 à 600 ; défaut : 15). Pour compiler : `dotnet build -c Release`.

## Entrées

Deux tableaux JSON de personnes, respectivement `hommes.json` et `femmes.json`. Exemple :

```json
[
  {
    "Prenom": "Alexandre",
    "Nom": "Martin",
    "Age": 31,
    "TranchesRecherchees": ["25-30", "30-35"]
  }
]
```

Les tranches autorisées sont : `20-25`, `25-30`, `30-35`, `35-40`, `40-45`, `45-50`, `45-55`, `55-60`, `60-65`, `65-70`, `70-75`. Les intervalles de recherche sont demi-ouverts, sauf `70-75` qui inclut 75. Les doublons de tranches sont éliminés.

## Déroulement des trois phases

### Phase 1 — Optimisation CP-SAT (inchangée)

Le solveur crée des tables équilibrées de 2H/2F à 5H/5F, en exigeant une compatibilité réciproque entre chaque homme et chaque femme de la table. Il maximise d'abord le nombre de personnes placées, puis favorise les tables de plus grande taille. Un statut `Feasible` ne prouve pas l'optimalité, contrairement à `Optimal`.

### Phase 2 — Placement dans les bornes d'âge

Chaque personne non placée peut rejoindre **une table existante** si son âge appartient à l'intervalle **inclusif** `[âge minimum, âge maximum]` des personnes du sexe opposé présentes à cette table. Exemple : un homme de 31 ans peut rejoindre une table où les femmes ont entre 20 et 41 ans, mais pas entre 20 et 28 ans. Les tranches recherchées ne sont plus exigées à ce stade.

L'algorithme recalcule les intervalles après chaque affectation. Il traite en priorité les personnes ayant le moins de tables admissibles ; parmi les tables admissibles, il choisit celle avec le plus petit effectif total, puis le plus petit effectif du même sexe, puis le plus petit numéro de table. Cela répartit les nouveaux participants plutôt que de remplir systématiquement la première table. C'est une heuristique d'équilibrage, pas une preuve d'équilibre global optimal.

### Phase 3 — Placement par proximité d'âge

Pour chaque personne encore en attente, on mesure sa distance à l'intervalle d'âge des personnes du sexe opposé de chaque table : `0` à l'intérieur de l'intervalle, `min - âge` en dessous, `âge - max` au-dessus. On choisit la distance minimale ; en cas d'égalité, on favorise la table la moins remplie, puis le plus petit effectif du même sexe, puis son numéro. Les bornes sont recalculées après chaque affectation.

## Limitations et garanties

- Les phases 2 et 3 **n'imposent ni compatibilité réciproque, ni équilibre hommes/femmes, ni capacité maximale de dix personnes**. Elles peuvent modifier les effectifs et les intervalles des tables initiales, sans déplacer les personnes déjà affectées.
- **Aucune table supplémentaire n'est créée** durant les phases 2 et 3. S'il n'existe aucune table CP-SAT, les personnes restent non placées. De même, une table sans membre du sexe opposé ne permet pas de calculer la distance requise.
- Chaque personne est affectée au plus une fois ; une vérification finale contrôle l'exhaustivité des indices et l'absence de doublons.
- Le programme affiche les nombres placés dans chacune des trois phases et les éventuels participants restant en attente.
- Les nouvelles phases sont déterministes et gloutonnes ; elles ne garantissent pas la meilleure répartition globale.
- Le temps maximal fourni sur la ligne de commande ne limite que la recherche CP-SAT, pas les phases de rattrapage.

## Organisation

- `Program.cs` : lecture, validation, orchestration et affichage.
- `Models.cs` : objets métiers et résultat.
- `RepartiteurCpSat.cs` : modèle CP-SAT initial.
- `PlacementComplementaire.cs` : phases 2 et 3, équilibrage et contrôles.

## Validation

Le SDK .NET n'étant pas installé dans l'environnement de génération, le projet n'a pas été compilé ni exécuté ici. Exécuter `dotnet restore` puis `dotnet build` et tester avec des données réelles avant utilisation.
