using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace RencontresTables;

internal static class Program
{
    // Intervalle 45-50 volontairement inclus dans 45-55, comme dans le code d'origine.
    private static readonly HashSet<string> TranchesAutorisees = new(StringComparer.Ordinal)
    {
        "20-25", "25-30", "30-35", "35-40", "40-45",
        "45-50", "45-55", "55-60", "60-65", "65-70", "70-75"
    };

    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            var fichierHommes = args.Length >= 1 ? args[0] : "hommes.json";
            var fichierFemmes = args.Length >= 2 ? args[1] : "femmes.json";
            // Contrairement à l'ancienne version : 3e argument = temps maximal en secondes.
            var limiteSecondes = args.Length >= 3
                ? double.Parse(args[2], CultureInfo.InvariantCulture)
                : 15.0;

            if (!double.IsFinite(limiteSecondes) || limiteSecondes < 0.1 || limiteSecondes > 600)
                throw new ArgumentException("La durée maximale doit être entre 0.1 et 600 secondes.");

            var hommes = Charger(fichierHommes);
            var femmes = Charger(fichierFemmes);
            var repartiteur = new RepartiteurCpSat(hommes, femmes);
            var resultatInitial = repartiteur.Repartir(limiteSecondes);
            var bilan = PlacementComplementaire.Completer(resultatInitial, hommes, femmes);
            var resultat = bilan.Resultat;

            Console.WriteLine();
            EcrireCouleur("========== RÉSULTAT DE LA RÉPARTITION ==========" , ConsoleColor.Yellow);
            Console.WriteLine("Phase 1 (CP-SAT)              : {0} personnes", resultatInitial.NombrePlaces);
            Console.WriteLine("Phase 2 (bornes d'âge)       : +{0} personnes", bilan.Phase2);
            Console.WriteLine("Phase 3 (âge le plus proche) : +{0} personnes", bilan.Phase3);
            Console.WriteLine("Nombre de personnes totales     : {0}", hommes.Count + femmes.Count);
            Console.WriteLine("Nombre de tables créées         : {0}", resultat.Tables.Count);
            Console.WriteLine("Nombre de personnes placées     : {0}", resultat.NombrePlaces);
            Console.WriteLine("Nombre de personnes non placées : {0}",
                resultat.HommesNonPlaces.Count + resultat.FemmesNonPlacees.Count);
            Console.WriteLine("Solveur CP-SAT : {0} | Optimalité prouvée : {1} | Calcul : {2:F3} s",
                resultat.StatutSolveur, resultat.OptimaliteProuvee ? "oui" : "non", resultat.DureeSecondes);
            if (!resultat.OptimaliteProuvee)
                EcrireCouleur("ATTENTION : le résultat est valide, mais son optimalité n'est pas garantie.",
                    ConsoleColor.DarkYellow);

            Console.WriteLine();
            EcrireCouleur("========== PERSONNES NON PLACÉES ==========" , ConsoleColor.Yellow);
            Console.WriteLine("Femmes ({0}) :", resultat.FemmesNonPlacees.Count);
            if (resultat.FemmesNonPlacees.Count == 0) Console.WriteLine("Aucune");
            foreach (var index in resultat.FemmesNonPlacees)
                EcrirePersonneAvecRaison(femmes[index],
                    "Aucune table existante ne possède d’homme permettant de calculer un intervalle d’âge.", ConsoleColor.Magenta);

            Console.WriteLine();
            Console.WriteLine("Hommes ({0}) :", resultat.HommesNonPlaces.Count);
            if (resultat.HommesNonPlaces.Count == 0) Console.WriteLine("Aucun");
            foreach (var index in resultat.HommesNonPlaces)
                EcrirePersonneAvecRaison(hommes[index],
                    "Aucune table existante ne possède de femme permettant de calculer un intervalle d’âge.", ConsoleColor.Cyan);

            Console.WriteLine();
            for (var numero = 0; numero < resultat.Tables.Count; numero++)
            {
                var table = resultat.Tables[numero];
                EcrireCouleur($"---------- Table {numero + 1} ({table.Hommes.Length}H / {table.Femmes.Length}F) ----------",
                    ConsoleColor.Yellow);
                Console.WriteLine("Femmes :");
                foreach (var index in table.Femmes) EcrireCouleur(femmes[index].NomComplet, ConsoleColor.Magenta);
                Console.WriteLine("Hommes :");
                foreach (var index in table.Hommes) EcrireCouleur(hommes[index].NomComplet, ConsoleColor.Cyan);
                EcrireCouleur($"---------- Fin table {numero + 1} ----------", ConsoleColor.Yellow);
                Console.WriteLine();
            }
            return 0;
        }
        catch (Exception ex)
        {
            EcrireCouleur("Erreur : " + ex.Message, ConsoleColor.Red);
            Console.Error.WriteLine(
                "Utilisation : RencontresTables.exe [hommes.json] [femmes.json] [dureeMaxSecondes]");
            return 1;
        }
    }

    private static List<Personne> Charger(string chemin)
    {
        if (!File.Exists(chemin))
            throw new FileNotFoundException("Fichier introuvable : " + Path.GetFullPath(chemin));

        var personnes = JsonConvert.DeserializeObject<List<Personne>>(
            File.ReadAllText(chemin, Encoding.UTF8));
        if (personnes is null)
            throw new InvalidDataException($"Le fichier {chemin} ne contient pas une liste JSON.");

        for (var i = 0; i < personnes.Count; i++)
        {
            var p = personnes[i];
            if (p is null)
                throw new InvalidDataException($"Personne null à la position {i + 1} dans {chemin}.");
            if (string.IsNullOrWhiteSpace(p.Prenom) || string.IsNullOrWhiteSpace(p.Nom))
                throw new InvalidDataException($"Prénom ou nom manquant à la position {i + 1} dans {chemin}.");
            if (p.Age <= 0 || p.Age > 120)
                throw new InvalidDataException($"Âge invalide pour {p.NomComplet} dans {chemin}.");
            if (p.TranchesRecherchees is null)
                throw new InvalidDataException($"TranchesRecherchees manquant pour {p.NomComplet}.");
            foreach (var tranche in p.TranchesRecherchees)
                if (tranche is null || !TranchesAutorisees.Contains(tranche))
                    throw new InvalidDataException($"Tranche inconnue '{tranche}' pour {p.NomComplet}.");
            p.TranchesRecherchees = p.TranchesRecherchees.Distinct(StringComparer.Ordinal).ToList();
        }
        return personnes;
    }

    private static void EcrirePersonneAvecRaison(Personne personne, string raison, ConsoleColor couleur)
    {
        var precedente = Console.ForegroundColor;
        try { Console.ForegroundColor = couleur; Console.Write(personne.NomComplet); }
        finally { Console.ForegroundColor = precedente; }
        Console.WriteLine(" -> " + raison);
    }

    private static void EcrireCouleur(string texte, ConsoleColor couleur)
    {
        var precedente = Console.ForegroundColor;
        try { Console.ForegroundColor = couleur; Console.WriteLine(texte); }
        finally { Console.ForegroundColor = precedente; }
    }
}
