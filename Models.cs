namespace RencontresTables;

internal sealed class Personne
{
    public string Prenom { get; set; } = "";
    public string Nom { get; set; } = "";
    public int Age { get; set; }
    // Null indique un champ absent dans le JSON : cette situation est rejetée.
    public List<string>? TranchesRecherchees { get; set; }
    public string NomComplet => $"{Prenom} {Nom}";
}

internal sealed record TableRencontre(int[] Hommes, int[] Femmes);

internal sealed record Resultat(
    List<TableRencontre> Tables,
    List<int> HommesNonPlaces,
    List<int> FemmesNonPlacees,
    string StatutSolveur,
    bool OptimaliteProuvee,
    double DureeSecondes)
{
    public int NombrePlaces => Tables.Sum(t => t.Hommes.Length + t.Femmes.Length);
}

internal sealed record PlageAge(string Nom, int Minimum, int MaximumExclusif);

/// <summary>Table finale : les ensembles complementaires identifient les ajouts post CP-SAT.</summary>
internal sealed record TableFinale(
    int[] Hommes,
    int[] Femmes,
    HashSet<int> HommesComplementaires,
    HashSet<int> FemmesComplementaires);

internal sealed record ResultatFinal(
    List<TableFinale> Tables,
    int NombrePlacesCpSat,
    int AjoutesParIntervalle,
    int AjoutesParProximite,
    int AjoutesDansNouvellesTables,
    string StatutSolveur,
    bool OptimaliteProuvee,
    double DureeSecondes)
{
    public int NombrePlaces => Tables.Sum(t => t.Hommes.Length + t.Femmes.Length);
    public int NombreAjoutes => AjoutesParIntervalle + AjoutesParProximite + AjoutesDansNouvellesTables;
}
