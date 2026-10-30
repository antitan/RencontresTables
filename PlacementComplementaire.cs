namespace RencontresTables;

/// <summary>
/// Deuxième étape, après l'optimisation stricte CP-SAT : tous les inscrits restants
/// sont affectés sans dépasser cinq personnes de chaque sexe par table.
/// La couleur jaune permettra de distinguer ces ajouts des placements CP-SAT.
///
/// Phase 2 : l'âge du participant est dans l'intervalle des âges des personnes
/// du sexe opposé déjà présentes à la table.
/// Phase 3 : sinon, utiliser la table ayant l'intervalle opposé le plus proche.
/// Phase 4 : si les tables existantes sont pleines, créer d'autres tables.
///
/// Seule la phase CP-SAT impose la compatibilité réciproque et la parité.
/// Les phases suivantes sont des affectations de secours assumées.
/// </summary>
internal sealed class PlacementComplementaire
{
    private const int MaximumParSexe = 5;

    private readonly IReadOnlyList<Personne> _hommes;
    private readonly IReadOnlyList<Personne> _femmes;

    public PlacementComplementaire(IReadOnlyList<Personne> hommes, IReadOnlyList<Personne> femmes)
    {
        _hommes = hommes;
        _femmes = femmes;
    }

    public ResultatFinal Completer(Resultat cpSat)
    {
        var tables = cpSat.Tables.Select(t => new TableConstruction(t.Hommes, t.Femmes)).ToList();
        var restantsH = cpSat.HommesNonPlaces.ToHashSet();
        var restantsF = cpSat.FemmesNonPlacees.ToHashSet();
        var initial = cpSat.NombrePlaces;

        // Phase 2 : âge dans l'intervalle des personnes du sexe opposé.
        int parIntervalle = AffecterAuxTablesExistantes(tables, restantsH, restantsF, true);
        // Phase 3 : âge le plus proche des intervalles du sexe opposé.
        int parProximite = AffecterAuxTablesExistantes(tables, restantsH, restantsF, false);
        // Phase 4 : tables supplémentaires si aucune place n'est disponible.
        int parNouvellesTables = CreerTablesSupplementaires(tables, restantsH, restantsF);

        if (restantsH.Count != 0 || restantsF.Count != 0)
            throw new InvalidOperationException("La répartition complémentaire a laissé des personnes sans table.");

        var tousH = tables.SelectMany(t => t.Hommes).ToArray();
        var tousF = tables.SelectMany(t => t.Femmes).ToArray();
        if (tousH.Length != _hommes.Count || tousF.Length != _femmes.Count ||
            tousH.Distinct().Count() != _hommes.Count ||
            tousF.Distinct().Count() != _femmes.Count ||
            tables.Any(t => t.Hommes.Count > MaximumParSexe || t.Femmes.Count > MaximumParSexe))
            throw new InvalidOperationException("Répartition incorrecte : doublon, personne manquante ou table surchargée.");

        return new ResultatFinal(
            tables.Select(t => new TableFinale(
                t.Hommes.ToArray(), t.Femmes.ToArray(),
                t.HommesComplementaires.ToHashSet(),
                t.FemmesComplementaires.ToHashSet())).ToList(),
            initial, parIntervalle, parProximite, parNouvellesTables,
            cpSat.StatutSolveur, cpSat.OptimaliteProuvee, cpSat.DureeSecondes);
    }

    private int AffecterAuxTablesExistantes(
        List<TableConstruction> tables,
        HashSet<int> restantsH,
        HashSet<int> restantsF,
        bool dansIntervalle)
    {
        int ajoutes = 0;
        while (true)
        {
            // On cherche globalement la meilleure affectation disponible,
            // en alternant implicitement les sexes selon les scores.
            OptionPlacement? meilleur = null;
            foreach (bool femme in new[] { true, false })
            {
                var indices = femme ? restantsF : restantsH;
                foreach (int index in indices.OrderBy(x => x))
                {
                    Personne personne = femme ? _femmes[index] : _hommes[index];
                    for (int t = 0; t < tables.Count; t++)
                    {
                        var table = tables[t];
                        int memeSexe = femme ? table.Femmes.Count : table.Hommes.Count;
                        if (memeSexe >= MaximumParSexe)
                            continue;

                        var agesOpposes = femme
                            ? table.Hommes.Select(h => _hommes[h].Age).ToArray()
                            : table.Femmes.Select(f => _femmes[f].Age).ToArray();
                        if (agesOpposes.Length == 0)
                            continue;

                        int min = agesOpposes.Min();
                        int max = agesOpposes.Max();
                        int distance = personne.Age < min ? min - personne.Age
                            : personne.Age > max ? personne.Age - max : 0;
                        if (dansIntervalle && distance != 0)
                            continue;
                        if (!dansIntervalle && distance == 0)
                            continue; // Les placements dans l'intervalle ont déjà été tentés.

                        int compatibilites = femme
                            ? table.Hommes.Count(h => EstCompatible(_hommes[h], personne))
                            : table.Femmes.Count(f => EstCompatible(personne, _femmes[f]));
                        int desequilibre = agesOpposes.Length - memeSexe;
                        var choix = new OptionPlacement(femme, index, t, distance,
                            compatibilites, desequilibre, table.Hommes.Count + table.Femmes.Count);
                        if (meilleur is null || EstMeilleur(choix, meilleur, dansIntervalle))
                            meilleur = choix;
                    }
                }
            }

            if (meilleur is null)
                break;
            var destination = tables[meilleur.Table];
            if (meilleur.EstFemme)
            {
                destination.Femmes.Add(meilleur.Index);
                destination.FemmesComplementaires.Add(meilleur.Index);
                restantsF.Remove(meilleur.Index);
            }
            else
            {
                destination.Hommes.Add(meilleur.Index);
                destination.HommesComplementaires.Add(meilleur.Index);
                restantsH.Remove(meilleur.Index);
            }
            ajoutes++;
        }
        return ajoutes;
    }

    private static bool EstMeilleur(OptionPlacement a, OptionPlacement b, bool dansIntervalle)
    {
        // Phase 2 : préférer les tables offrant le plus de compatibilités réciproques.
        // Phase 3 : préférer avant tout le plus petit écart à l'intervalle.
        if (!dansIntervalle && a.Distance != b.Distance)
            return a.Distance < b.Distance;
        if (a.Compatibilites != b.Compatibilites)
            return a.Compatibilites > b.Compatibilites;
        if (a.Desequilibre != b.Desequilibre)
            return a.Desequilibre > b.Desequilibre;
        if (a.Taille != b.Taille)
            return a.Taille > b.Taille;
        if (a.Table != b.Table)
            return a.Table < b.Table;
        if (a.EstFemme != b.EstFemme)
            return a.EstFemme;
        return a.Index < b.Index;
    }

    private int CreerTablesSupplementaires(
        List<TableConstruction> tables,
        HashSet<int> restantsH,
        HashSet<int> restantsF)
    {
        int total = restantsH.Count + restantsF.Count;
        if (total == 0)
            return 0;

        // Limite de cinq hommes et cinq femmes par table. On répartit les restants
        // le plus équitablement possible entre les nouvelles tables.
        int nouvellesTables = Math.Max(
            (restantsH.Count + MaximumParSexe - 1) / MaximumParSexe,
            (restantsF.Count + MaximumParSexe - 1) / MaximumParSexe);
        var ajouts = Enumerable.Range(0, nouvellesTables)
            .Select(_ => new TableConstruction(Array.Empty<int>(), Array.Empty<int>()))
            .ToArray();

        RepartirEquitablement(restantsH, _hommes, ajouts, false);
        RepartirEquitablement(restantsF, _femmes, ajouts, true);
        tables.AddRange(ajouts);
        restantsH.Clear();
        restantsF.Clear();
        return total;
    }

    private static void RepartirEquitablement(
        HashSet<int> indices,
        IReadOnlyList<Personne> personnes,
        TableConstruction[] tables,
        bool femme)
    {
        // Tri par âge pour rapprocher les profils au sein d'une même table.
        var tries = indices.OrderBy(i => personnes[i].Age).ThenBy(i => i).ToArray();
        int quotient = tries.Length / tables.Length;
        int reste = tries.Length % tables.Length;
        int position = 0;
        for (int t = 0; t < tables.Length; t++)
        {
            int effectif = quotient + (t < reste ? 1 : 0);
            for (int n = 0; n < effectif; n++)
            {
                int index = tries[position++];
                if (femme)
                {
                    tables[t].Femmes.Add(index);
                    tables[t].FemmesComplementaires.Add(index);
                }
                else
                {
                    tables[t].Hommes.Add(index);
                    tables[t].HommesComplementaires.Add(index);
                }
            }
        }
    }

    private static bool EstCompatible(Personne homme, Personne femme) =>
        AgeAccepte(femme.Age, homme.TranchesRecherchees!) &&
        AgeAccepte(homme.Age, femme.TranchesRecherchees!);

    private static bool AgeAccepte(int age, List<string> tranches)
    {
        // Même sémantique que dans RepartiteurCpSat, y compris le chevauchement
        // des tranches 45-50 / 45-55 et l'inclusion de 75 ans dans 70-75.
        if (age < 20 || age > 75)
            return false;
        foreach (string tranche in tranches)
        {
            string[] bornes = tranche.Split('-');
            int min = int.Parse(bornes[0]);
            int max = int.Parse(bornes[1]);
            if (age >= min && (age < max || (tranche == "70-75" && age == 75)))
                return true;
        }
        return false;
    }

    private sealed record OptionPlacement(bool EstFemme, int Index, int Table,
        int Distance, int Compatibilites, int Desequilibre, int Taille);

    private sealed class TableConstruction
    {
        public TableConstruction(IEnumerable<int> hommes, IEnumerable<int> femmes)
        {
            Hommes = hommes.ToList();
            Femmes = femmes.ToList();
        }

        public List<int> Hommes { get; }
        public List<int> Femmes { get; }
        public HashSet<int> HommesComplementaires { get; } = new();
        public HashSet<int> FemmesComplementaires { get; } = new();
    }
}
