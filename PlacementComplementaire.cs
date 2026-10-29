namespace RencontresTables;

/// <summary>Ajoute les personnes non placées aux tables existantes, sans modifier les affectations CP-SAT.</summary>
internal static class PlacementComplementaire
{
    internal sealed record Bilan(Resultat Resultat, int Phase2, int Phase3, int SansTable);

    public static Bilan Completer(Resultat initial, IList<Personne> hommes, IList<Personne> femmes)
    {
        var tables = initial.Tables.Select(t => new TableMutable(t)).ToList();
        var attente = initial.HommesNonPlaces.Select(i => (Femme: false, Index: i))
            .Concat(initial.FemmesNonPlacees.Select(i => (Femme: true, Index: i)))
            .ToList();
        int phase2 = 0, phase3 = 0;
        // Les bornes sont recalculées à chaque tour. Le meilleur candidat est celui qui
        // possède le moins de tables admissibles, pour éviter de bloquer les cas difficiles.
        while (attente.Count > 0 && tables.Count > 0)
        {
            var choix = attente.Select((p, position) => new
            {
                p.Femme, p.Index, Position = position,
                Tables = Enumerable.Range(0, tables.Count)
                    .Where(t => Distance(p.Femme, p.Index, tables[t], hommes, femmes) == 0)
                    .ToList()
            }).Where(c => c.Tables.Count > 0)
              .OrderBy(c => c.Tables.Count).ThenBy(c => c.Position).FirstOrDefault();
            if (choix is null) break;
            // Équilibrage sur l'effectif total, puis sur l'effectif du même sexe.
            int table = choix.Tables.OrderBy(t => tables[t].Total)
                .ThenBy(t => choix.Femme ? tables[t].Femmes.Count : tables[t].Hommes.Count)
                .ThenBy(t => t).First();
            Ajouter(tables[table], choix.Femme, choix.Index);
            attente.RemoveAt(choix.Position);
            phase2++;
        }

        // Phase 3 : distance à l'intervalle fermé [min,max] du sexe opposé.
        // Une table sans personne du sexe opposé n'a pas d'intervalle calculable.
        foreach (var p in attente.ToArray())
        {
            var candidats = Enumerable.Range(0, tables.Count)
                .Select(t => (Table: t, Distance: Distance(p.Femme, p.Index, tables[t], hommes, femmes)))
                .Where(x => x.Distance.HasValue)
                .OrderBy(x => x.Distance!.Value)
                .ThenBy(x => tables[x.Table].Total)
                .ThenBy(x => p.Femme ? tables[x.Table].Femmes.Count : tables[x.Table].Hommes.Count)
                .ThenBy(x => x.Table).ToList();
            if (candidats.Count == 0) continue;
            Ajouter(tables[candidats[0].Table], p.Femme, p.Index);
            attente.Remove(p);
            phase3++;
        }
        var resultat = initial with
        {
            Tables = tables.Select(t => new TableRencontre(t.Hommes.ToArray(), t.Femmes.ToArray())).ToList(),
            HommesNonPlaces = attente.Where(p => !p.Femme).Select(p => p.Index).ToList(),
            FemmesNonPlacees = attente.Where(p => p.Femme).Select(p => p.Index).ToList()
        };
        Verifier(resultat, hommes.Count, femmes.Count);
        return new Bilan(resultat, phase2, phase3, attente.Count);
    }

    private static int? Distance(bool femme, int index, TableMutable table,
        IList<Personne> hommes, IList<Personne> femmes)
    {
        var opposants = femme ? table.Hommes.Select(i => hommes[i].Age)
                              : table.Femmes.Select(i => femmes[i].Age);
        var ages = opposants.ToArray();
        if (ages.Length == 0) return null;
        int age = femme ? femmes[index].Age : hommes[index].Age;
        int min = ages.Min(), max = ages.Max();
        return age < min ? min - age : age > max ? age - max : 0;
    }

    private static void Ajouter(TableMutable table, bool femme, int index)
    {
        if (femme) table.Femmes.Add(index);
        else table.Hommes.Add(index);
    }

    private static void Verifier(Resultat resultat, int nbHommes, int nbFemmes)
    {
        var h = resultat.Tables.SelectMany(t => t.Hommes).Concat(resultat.HommesNonPlaces).ToArray();
        var f = resultat.Tables.SelectMany(t => t.Femmes).Concat(resultat.FemmesNonPlacees).ToArray();
        if (h.Length != nbHommes || f.Length != nbFemmes ||
            h.Distinct().Count() != nbHommes || f.Distinct().Count() != nbFemmes ||
            h.Any(i => i < 0 || i >= nbHommes) || f.Any(i => i < 0 || i >= nbFemmes))
            throw new InvalidDataException("Répartition incomplète ou indices dupliqués.");
    }

    private sealed class TableMutable(TableRencontre table)
    {
        public List<int> Hommes { get; } = table.Hommes.ToList();
        public List<int> Femmes { get; } = table.Femmes.ToList();
        public int Total => Hommes.Count + Femmes.Count;
    }
}
