using System;
using Sudoku.Shared;

namespace Sudoku.ParticleSwarmOptimization
{

	//Quelques remarques ici concernant votre première implémentation: votre solver résout péniblement le sudokus les plus faciles alors je pense qu'il y a une bonne marge de progression.
	//Je n'ai pas forcément compris éxactement comment vous avez implémenté votre solver, mais j'ai l'impression que vous vous êtes inspiré de l'un des exemples simples proposés, et que vous l'avez adapté à votre sauce.
	//Bon pour le coup, les performances n'étaient pas exceptionnelles et je crois qu'avec votre implémentation elles se sont dégradées.

	// Alors déjà, vous pourriez tenter de retomber sur vos pattes en profilant l'exécution de votre code et celle des solvers en question, en utilisant typiquement Jetbrains DotTrace qui fonctionne très bien, et vous permettra le cas échéant de faire la chasse à tout ce qui prend du temps dans votre code.
	//Ceci dit, ce faisant ce n'est pas dit que vous aboutissiez à quelque chose de bien plus performant que l'orginal, qui n'est en soit pas extraordinaire.





	// Je conseillerais plutôt de choisir une autre piste.


	// Il me semble qu'on en a discuté en cours, et le premier obstacle évoqué est que le PSO est généralement utilisé sur des problèmes d'optimisation à valeurs réelles, et il n'est pas forcément trivial de l'adapter à un problème comme le Sudoku.
	// Pourtant il existe pas mal de littérature sur l'utilisation du PSO pour résoudre des problèmes combinatoires, typiquement le TSP (trouver l'ordre des villes à visiter pour minimiser la longueur de la tournée)
	// CF https://scholar.google.fr/scholar?q=Particle+Swarm+Optimisation+Travelling+Salesman+Problem
	// Il y a par exemple ces code en c# même si je ne suis pas sûr de ce qu'ils valent:
	// https://www.codeproject.com/Articles/1182794/Solving-the-Travelling-Salesman-Problem-With-a-Par
	// https://github.com/RohanChhipa/TSPPSO
	// https://github.com/liuxx532/TspPsoDemo

	// Bon alors du coup si on veut s'appuyer sur ce genre d'exemple, ça serait pas mal de transformer le Sudoku en un problème similaire au TSP de type combinatoire,
	// voilà ce que je propose pour ce faire (comme nous en avaons discuté avec votre camarade sur les GeneticAlgo):
	// Pour un TSP à 81 villes, on cherche une permutation des nombres de 1 à 81, mais pour le Sudoku, on a 9 fois les chiffres de 1 à 9. Ceci dit, comme chaque chiffre apparait 9 fois, on peut leur attribuer arbitrairement un index de 1 à 9, tel que chaque cellule contienne un nombre de 1 à 81 qui vaut 9 * cet indice+ le chiffre, et le chiffre correspondant dans le Sudoku sera le nombre résultant modulo 9 (pour éviter de faire cette opération des millions de fois, je lui ai conseillé de stocker le résultat des modulo une fois pour toutes dans un tableau de 81 entiers). Et puis à y regarder de plus près, vous pouvez sans doute sortir le masque du Sudoku à résoudre histoire de ne pas rajouter des variables inutiles, mais c'est un détail d'implémentation pas trop dur à rajouter (avec un autre tableau de lookup entre l'index des paramètres de particule et l'index des cellules vides/hors masque du Sudoku)
	// Voilà donc déjà comment transformer sans trop de difficulté notre problème initial en un problème très proche du TSP et du coup bénéficier de tout ce qui a été dit et fait sur PSO+TSP

	//Après, pour une implémentation en c#, si l'un des codes ci-dessus est efficace alors tant mieux, mais sinon je resterais partisant d'utiliser une lib prévue pour ça plutôt qu'une implémentation custom du PSO exotique, au risque de retomber sur quelque chose de sous-optimal


	//En c#, je vous ai listé 2 libs: Métaheuristics et HeuristicLabs
	// Pour la plus simple, Métaheuristics, il se trouve qu'ils proposent du PSO discret, et je viens de faire le test dans cet ordre des 3 lignes suivantes correspondant à des variantes du PSO pour le TSP en commentant les autres lignes de test et en rajoutant un Stopwatch pour mesurer les perfs:
	//https://github.com/yasserglez/metaheuristics/blob/master/Test/Main.cs#L63
	//https://github.com/yasserglez/metaheuristics/blob/master/Test/Main.cs#L61
	//https://github.com/yasserglez/metaheuristics/blob/master/Test/Main.cs#L59
	//l'algo met chez moi autour de 4.5s pour les 3 pour leur TSP de 125 villes, avec pour distances respectivement 126959,272374665, 125195,353725608 et 125098,218900574 (la troisième solution semble la meilleur)
	// J'ai comparé à d'autres Métaheurstiques: pour une durée équivalente le GA donne en distance minimale de chemin 183202,997295795, le Simulated Annealing 135751,778048252 et le Ant Colony 131765,169479299, donc le PSO est clairement compétitif

	////Reste à savoir ce que la lib vaut dans l'absolu. Je pensais initialement qu'HeuristicLabs seraient plus performante, et j'allais vous donner des pistes pour utiliser cette lib, plus complète a priori, mais plus complexe à comprendre et manipuler (beaucoup, peut-être trop d'abstractions...).
	//Il se trouve que malheurusement l'exemple PSO+TSP n'est pas fourni dans HeuristicLab, et seul un exemple d'optimisation réelle est fourni, mais des exemples de TSP sont fournis pour d'autres Métaheuristiques et il serait possible de rajouter les opérateurs de particule à moindre coût une fois la mécanique complexe bien assimilée. Du coup j'ai récupéré l'exemple de TSP d'HeuristicsLab que j'ai transformé pour pouvoir le réutiliser dans Métaheuristics,
	// Le résultat est le suivant: Dans la lib Métaheuristic, le PSO optimisé trouve sur ce nouveau TSP de 130 villes plus proches une distance minimale de chemin de 7520,37301172668, et le GA de 10987,6210619243 pour une durée similaire de 4.5 sec, là ou le GA de HeuristicLabs trouve une distance de 12654 en 6 sec, l'Island GA ne fait pas mieux, le Tabu Search en revanche trouve 6286 (soit presque la distance optimale de 6110) en 15s
	// Bilan des courses: il est possible que HeuristicLabs soit plus performante, car ici la comparaison est un peu biaisée parceque l'UI d'heuristiclabs est hyper-instrumentée avec tout un tas d'évenements tandis que chez Metaheuristics le code est dénué de fioritures.
	// Il est possible également que le PSO d'heuristiclabs soit ultra performant à l'image du TabuSearch, mais les résultats qui sont comparables à savoir ceux sur le GA simple me laissent penser qu'il serait plus judicieux de partir sur le PSO de Metaheuristics qui se défent très bien et dont l'adaptation devrait être assez facile.

	// Bon je vous laisse déjà digérer tout ça, et je suis à votre disposition pour détailler mon idée de comment transformer le TSP en Sudoku pour pouvoir utiliser le PSO, et pour répondre à vos questions sur les librairies



	public class PSOSolver : ISudokuSolver
    {
        public static SudokuGrid original = new SudokuGrid();
        public SudokuGrid Solve(SudokuGrid s)
        {
            // Initialization of the hive
            original = s;
            var hive = new Hive(1, 100);
            return hive.Solve();
        }
    }
}