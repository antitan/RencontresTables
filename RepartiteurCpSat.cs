using Google.OrTools.Sat;
using System.Diagnostics;

namespace RencontresTables;

/// <summary>
/// Affectation exacte (si CpSolverStatus.Optimal) en bicliques équilibrées K(k,k), k=2..5.
/// Tous les hommes d'une table sont compatibles avec toutes les femmes, et réciproquement.
/// Les profils ayant les mêmes partenaires compatibles sont regroupés pour réduire le modèle.
/// </summary>
internal sealed class RepartiteurCpSat
{
    private readonly IList<Personne> _hommes;
    private readonly IList<Personne> _femmes;
    private readonly bool[,] _compatibles;

    private static readonly PlageAge[] Plages =
    {
        new("20-25", 20, 25), new("25-30", 25, 30),
        new("30-35", 30, 35), new("35-40", 35, 40),
        new("40-45", 40, 45), new("45-50", 45, 50),
        new("45-55", 45, 55), new("55-60", 55, 60),
        new("60-65", 60, 65), new("65-70", 65, 70),
        new("70-75", 70, 76)
    };

    public RepartiteurCpSat(IList<Personne> hommes, IList<Personne> femmes)
    {
        _hommes = hommes;
        _femmes = femmes;
        _compatibles = new bool[hommes.Count, femmes.Count];
        for (int h = 0; h < hommes.Count; h++)
            for (int f = 0; f < femmes.Count; f++)
                _compatibles[h, f] =
                    CorrespondAuxTranches(femmes[f].Age, hommes[h].TranchesRecherchees) &&
                    CorrespondAuxTranches(hommes[h].Age, femmes[f].TranchesRecherchees);
    }

    private static bool AgePrisEnCharge(int age) => age is >= 20 and <= 75;

    private static bool CorrespondAuxTranches(int age, IList<string>? tranches) =>
        AgePrisEnCharge(age) && tranches is not null &&
        Plages.Any(p => age >= p.Minimum && age < p.MaximumExclusif &&
                        tranches.Contains(p.Nom));

    // Deux individus du même sexe sont interchangeables si leur vecteur de
    // compatibilité avec l'ensemble du sexe opposé est identique.
    private List<int[]> GroupesHommes() => Enumerable.Range(0, _hommes.Count)
        .Where(h => Enumerable.Range(0, _femmes.Count).Any(f => _compatibles[h, f]))
        .GroupBy(h => new string(Enumerable.Range(0, _femmes.Count)
            .Select(f => _compatibles[h, f] ? '1' : '0').ToArray()))
        .Select(g => g.ToArray())
        .ToList();

    private List<int[]> GroupesFemmes() => Enumerable.Range(0, _femmes.Count)
        .Where(f => Enumerable.Range(0, _hommes.Count).Any(h => _compatibles[h, f]))
        .GroupBy(f => new string(Enumerable.Range(0, _hommes.Count)
            .Select(h => _compatibles[h, f] ? '1' : '0').ToArray()))
        .Select(g => g.ToArray())
        .ToList();

    public Resultat Repartir(double dureeMaxSecondes)
    {
        var chrono = Stopwatch.StartNew();
        var groupesH = GroupesHommes();
        var groupesF = GroupesFemmes();
        int nbTablesMax = Math.Min(_hommes.Count, _femmes.Count) / 2;

        if (nbTablesMax == 0 || groupesH.Count == 0 || groupesF.Count == 0)
            return ResultatVide("Optimal (aucune table possible)", true, chrono.Elapsed.TotalSeconds);

        var model = new CpModel();
        int typesH = groupesH.Count;
        int typesF = groupesF.Count;

        // quantiteH[h,t] : nombre de personnes du profil h affectées à la table t (0..5).
        // presentH[h,t] : au moins un membre de ce profil est présent à la table t.
        var quantiteH = new IntVar[typesH, nbTablesMax];
        var quantiteF = new IntVar[typesF, nbTablesMax];
        var presentH = new BoolVar[typesH, nbTablesMax];
        var presentF = new BoolVar[typesF, nbTablesMax];
        var tailles = new IntVar[nbTablesMax];
        var choixTaille = new BoolVar[nbTablesMax, 4]; // k=2,3,4,5

        for (int t = 0; t < nbTablesMax; t++)
        {
            tailles[t] = model.NewIntVar(0, 5, $"taille_{t}");
            var taillesPossibles = new List<ILiteral>();
            var expressionTaille = new List<LinearExpr>();
            for (int k = 2; k <= 5; k++)
            {
                var selection = model.NewBoolVar($"t{t}_taille{k}");
                choixTaille[t, k - 2] = selection;
                taillesPossibles.Add(selection);
                expressionTaille.Add(k * selection);
            }
            model.AddAtMostOne(taillesPossibles); // table inactive ou exactement 2..5 par sexe
            model.Add(tailles[t] == LinearExpr.Sum(expressionTaille));

            var hommesTable = new List<LinearExpr>();
            var femmesTable = new List<LinearExpr>();
            for (int h = 0; h < typesH; h++)
            {
                var q = model.NewIntVar(0, Math.Min(5, groupesH[h].Length), $"qh_{h}_{t}");
                var p = model.NewBoolVar($"ph_{h}_{t}");
                quantiteH[h, t] = q;
                presentH[h, t] = p;
                model.Add(q >= p);
                model.Add(q <= 5 * p);
                hommesTable.Add(q);
            }
            for (int f = 0; f < typesF; f++)
            {
                var q = model.NewIntVar(0, Math.Min(5, groupesF[f].Length), $"qf_{f}_{t}");
                var p = model.NewBoolVar($"pf_{f}_{t}");
                quantiteF[f, t] = q;
                presentF[f, t] = p;
                model.Add(q >= p);
                model.Add(q <= 5 * p);
                femmesTable.Add(q);
            }
            model.Add(LinearExpr.Sum(hommesTable) == tailles[t]);
            model.Add(LinearExpr.Sum(femmesTable) == tailles[t]);

            // Une paire de profils incompatible ne peut pas partager une même table.
            for (int h = 0; h < typesH; h++)
                for (int f = 0; f < typesF; f++)
                    if (!_compatibles[groupesH[h][0], groupesF[f][0]])
                        model.AddBoolOr(new ILiteral[]
                        {
                            presentH[h, t].Not(), presentF[f, t].Not()
                        });
        }

        // Toute personne d'un profil est utilisée au plus une fois.
        for (int h = 0; h < typesH; h++)
            model.Add(LinearExpr.Sum(Enumerable.Range(0, nbTablesMax)
                .Select(t => (LinearExpr)quantiteH[h, t])) <= groupesH[h].Length);
        for (int f = 0; f < typesF; f++)
            model.Add(LinearExpr.Sum(Enumerable.Range(0, nbTablesMax)
                .Select(t => (LinearExpr)quantiteF[f, t])) <= groupesF[f].Length);

        // Brise une symétrie importante : les tables sont triées par taille décroissante.
        for (int t = 1; t < nbTablesMax; t++)
            model.Add(tailles[t - 1] >= tailles[t]);

        // Objectif LEXICOGRAPHIQUE en une passe :
        // 1. maximiser les personnes (2 * somme des tailles);
        // 2. à nombre placé égal, maximiser les tables 5+5, puis 4+4, puis 3+3.
        // B > nombre maximal de tables garantit cette priorité exacte.
        // L'overflow est traité par une erreur explicite plutôt qu'un classement faux.
        long b, poidsPrincipal;
        try
        {
            checked
            {
                b = nbTablesMax + 1L;
                poidsPrincipal = b * b * b * b;
                // Vérifier également le plus grand score réalisable.
                _ = nbTablesMax * (10L * poidsPrincipal + b * b * b);
            }
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException(
                "Trop de tables pour les pondérations lexicographiques 64 bits. " +
                "Répartissez les créneaux ou utilisez une optimisation multi-étapes.");
        }

        var termesObjectif = new List<LinearExpr>();
        for (int t = 0; t < nbTablesMax; t++)
        {
            // Chaque taille k représente k hommes + k femmes.
            termesObjectif.Add((2L * poidsPrincipal) * tailles[t]);
            termesObjectif.Add(choixTaille[t, 3] * (b * b * b)); // 5+5
            termesObjectif.Add(choixTaille[t, 2] * (b * b));     // 4+4
            termesObjectif.Add(choixTaille[t, 1] * b);           // 3+3
            termesObjectif.Add(choixTaille[t, 0]);               // 2+2
        }
        model.Maximize(LinearExpr.Sum(termesObjectif));

        var erreurModele = model.Validate();
        if (!string.IsNullOrEmpty(erreurModele))
            throw new InvalidOperationException("Modèle CP-SAT invalide : " + erreurModele);

        var solveur = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{dureeMaxSecondes.ToString(System.Globalization.CultureInfo.InvariantCulture)} num_search_workers:8"
        };
        var statut = solveur.Solve(model);
        chrono.Stop();

        if (statut != CpSolverStatus.Optimal && statut != CpSolverStatus.Feasible)
            return ResultatVide(statut.ToString() + " (aucune solution disponible)", false,
                chrono.Elapsed.TotalSeconds);

        // Répartition des vraies personnes depuis les quantités par profils.
        var disponiblesH = groupesH.Select(g => new Queue<int>(g)).ToArray();
        var disponiblesF = groupesF.Select(g => new Queue<int>(g)).ToArray();
        var tables = new List<TableRencontre>();
        for (int t = 0; t < nbTablesMax; t++)
        {
            int k = (int)solveur.Value(tailles[t]);
            if (k == 0) continue;
            var indicesH = new List<int>();
            var indicesF = new List<int>();
            for (int h = 0; h < typesH; h++)
                for (int n = 0; n < solveur.Value(quantiteH[h, t]); n++)
                    indicesH.Add(disponiblesH[h].Dequeue());
            for (int f = 0; f < typesF; f++)
                for (int n = 0; n < solveur.Value(quantiteF[f, t]); n++)
                    indicesF.Add(disponiblesF[f].Dequeue());
            if (indicesH.Count != k || indicesF.Count != k || k < 2 || k > 5)
                throw new InvalidDataException("Une table invalide a été renvoyée par le solveur.");
            foreach (int h in indicesH)
                foreach (int f in indicesF)
                    if (!_compatibles[h, f])
                        throw new InvalidDataException("Incompatibilité constatée dans une table !");
            tables.Add(new TableRencontre(indicesH.ToArray(), indicesF.ToArray()));
        }

        var placesH = tables.SelectMany(t => t.Hommes).ToHashSet();
        var placesF = tables.SelectMany(t => t.Femmes).ToHashSet();
        if (tables.Sum(t => t.Hommes.Length) != placesH.Count ||
            tables.Sum(t => t.Femmes.Length) != placesF.Count)
            throw new InvalidDataException("Une personne a été placée dans plusieurs tables.");
        return new Resultat(tables,
            Enumerable.Range(0, _hommes.Count).Where(i => !placesH.Contains(i)).ToList(),
            Enumerable.Range(0, _femmes.Count).Where(i => !placesF.Contains(i)).ToList(),
            statut.ToString(), statut == CpSolverStatus.Optimal, chrono.Elapsed.TotalSeconds);
    }

    private Resultat ResultatVide(string statut, bool optimalite, double secondes) =>
        new(new List<TableRencontre>(), Enumerable.Range(0, _hommes.Count).ToList(),
            Enumerable.Range(0, _femmes.Count).ToList(), statut, optimalite, secondes);

    public string ExpliquerNonPlacement(bool estFemme, int index, Resultat resultat)
    {
        var personne = estFemme ? _femmes[index] : _hommes[index];
        var autres = estFemme ? _hommes : _femmes;

        if (!AgePrisEnCharge(personne.Age))
            return "Âge hors des tranches prises en charge (20 à 75 ans).";
        if (personne.TranchesRecherchees is null || personne.TranchesRecherchees.Count == 0)
            return "Aucune tranche d'âge recherchée renseignée.";
        if (autres.Count == 0)
            return estFemme ? "Aucun homme inscrit." : "Aucune femme inscrite.";

        int personnesDansTranches = autres.Count(p =>
            CorrespondAuxTranches(p.Age, personne.TranchesRecherchees));
        if (personnesDansTranches == 0)
        {
            var tranches = Plages.Where(p => personne.TranchesRecherchees.Contains(p.Nom)).ToArray();
            int ageMin = tranches.Min(p => p.Minimum);
            int ageMaxExcl = tranches.Max(p => p.MaximumExclusif);
            if (autres.All(p => p.Age < ageMin))
                return "TranchesRecherchees trop âgées : les personnes inscrites sont plus jeunes.";
            if (autres.All(p => p.Age >= ageMaxExcl))
                return "TranchesRecherchees pas assez âgées : les personnes inscrites sont plus âgées.";
            return "Aucune personne inscrite dans les tranches recherchées.";
        }

        var compatibles = Enumerable.Range(0, autres.Count)
            .Where(i => estFemme ? _compatibles[i, index] : _compatibles[index, i]).ToList();
        if (compatibles.Count == 0)
            return "Pas de compatibilité réciproque : votre âge ne correspond pas à leurs critères.";

        var autresNonPlaces = (estFemme ? resultat.HommesNonPlaces : resultat.FemmesNonPlacees).ToHashSet();
        var compatiblesRestants = compatibles.Count(autresNonPlaces.Contains);
        if (compatiblesRestants == 0)
            return "Toutes les personnes compatibles sont déjà affectées à d'autres tables.";
        if (resultat.HommesNonPlaces.Count < 2 || resultat.FemmesNonPlacees.Count < 2)
            return $"Effectif restant insuffisant pour une table (reste " +
                $"{resultat.HommesNonPlaces.Count} H / {resultat.FemmesNonPlacees.Count} F ; minimum 2 H / 2 F).";
        if (compatiblesRestants < 2)
            return "Une seule personne compatible encore disponible (il en faut au moins deux du sexe opposé).";
        if (!ExisteTableMinimumAvec(estFemme, index, resultat))
            return "Compatibilités croisées insuffisantes pour former une table de 2 hommes et 2 femmes.";

        return resultat.OptimaliteProuvee
            ? "Compatibilités résiduelles à examiner (une table supplémentaire semble possible : diagnostic à vérifier)."
            : "Une table compatible supplémentaire est possible, mais le solveur n'a pas prouvé l'optimalité dans le temps imparti.";
    }

    private bool ExisteTableMinimumAvec(bool estFemme, int index, Resultat resultat)
    {
        if (estFemme)
        {
            foreach (int autreFemme in resultat.FemmesNonPlacees)
            {
                if (autreFemme == index) continue;
                int hommesCommuns = 0;
                foreach (int homme in resultat.HommesNonPlaces)
                    if (_compatibles[homme, index] && _compatibles[homme, autreFemme] &&
                        ++hommesCommuns >= 2)
                        return true;
            }
        }
        else
        {
            foreach (int autreHomme in resultat.HommesNonPlaces)
            {
                if (autreHomme == index) continue;
                int femmesCommunes = 0;
                foreach (int femme in resultat.FemmesNonPlacees)
                    if (_compatibles[index, femme] && _compatibles[autreHomme, femme] &&
                        ++femmesCommunes >= 2)
                        return true;
            }
        }
        return false;
    }
}
