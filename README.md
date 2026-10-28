# RencontresTables — .NET 8 + Google OR-Tools CP-SAT

Application console C# qui répartit hommes et femmes dans des tables virtuelles.

## Prérequis

- .NET SDK 8 (x64 recommandé)
- NuGet accessible pendant la première restauration des packages
- Visual Studio 2022 (optionnel)

Dépendances NuGet : `Google.OrTools 9.15.6755`, `Newtonsoft.Json 13.0.3`.

## Démarrage

```bash
dotnet restore
dotnet run -- hommes.json femmes.json 15
```

Le 3e paramètre, facultatif, indique **le temps maximal accordé au solveur en secondes** (par défaut 15 ; entre 0.1 et 600). Ce n'est plus le nombre d'essais de l'ancienne heuristique.

Après compilation :

```bash
dotnet build -c Release
dotnet bin/Release/net8.0/RencontresTables.dll hommes.json femmes.json 15
```

Sur Windows les couleurs magenta/cyan sont gérées par `Console.ForegroundColor`.

## Règles conservées

- Compatibilité réciproque : pour chaque couple homme-femme d'une table, chacun accepte la tranche d'âge de l'autre.
- Chacun ne peut figurer que dans une seule table.
- Tables autorisées : 5H+5F, 4H+4F, 3H+3F et 2H+2F.
- Critère lexicographique : **placer le plus de personnes possible** ; à égalité, maximiser le nombre de tables de 10, puis 8, puis 6, puis 4.
- Tranches de l'ancienne application inchangées : y compris les plages qui se chevauchent `45-50` et `45-55`.
- Les 75 ans sont inclus dans la tranche `70-75` comme auparavant.
- Diagnostics sur les personnes non placées (diagnostics explicatifs, pas des preuves causales individuelles).

## Modèle mathématique

Le graphe de compatibilité est biparti. Les individus présentant les **mêmes voisins** (mêmes possibilités de compatibilité) sont regroupés en profils équivalents ; cela conserve l'optimalité tout en réduisant les variables lorsqu'il existe beaucoup de profils identiques.

Pour chaque type et chaque table : variable entière `quantite` (0..5) + booléen `present`. Pour chaque table : choix exclusif entre inactive, 2, 3, 4 ou 5 personnes de chaque sexe. Chaque type ne peut utiliser plus de personnes que son effectif. Une paire de types incompatibles est interdite sur la même table. La taille des tables est ordonnée pour réduire la symétrie.

L'objectif utilise des poids lexicographiques entiers vérifiés contre les débordements 64 bits. Le statut **Optimal** confirme que le nombre placé et les préférences de taille ont atteint l'optimum mathématique de ce modèle. **Feasible** signifie qu'une solution valide a été trouvée mais que son optimalité n'a pas été prouvée dans le délai fixé. **Unknown** peut survenir si le solveur n'a pas obtenu de solution exploitable dans le délai : dans ce cas aucune table n'est présentée comme optimisée.

La limite temporelle du solveur ne comprend pas nécessairement la construction du modèle ni la création des diagnostics.

## Conseils de montée en charge

Ce modèle exact peut devenir gourmand avec beaucoup de participants ayant des compatibilités toutes différentes (nombre de variables/contraintes dépend du nombre de types et du nombre de tables). Tester les charges réelles avant déploiement. Pour plusieurs milliers de participants, prévoir un traitement par lots et/ou une formulation plus spécialisée.

## Notes de validation

Fichiers JSON fournis à titre d'exemple. Le code a été préparé à partir de la documentation C# de OR-Tools 9.15. La compilation et l'exécution .NET ne sont pas réalisées dans l'environnement de génération dépourvu du SDK dotnet ; elles sont à confirmer localement.

## Exemple de données fourni

Les fichiers de démonstration contiennent 10 hommes et 10 femmes. La matrice de compatibilité construite à partir de leurs préférences a été vérifiée séparément par énumération combinatoire : l'optimum est de **14 personnes placées**, sous la forme d'une table de 10 et d'une table de 4. Cela valide l'instance de test, sans remplacer un test d'exécution du binaire .NET.
