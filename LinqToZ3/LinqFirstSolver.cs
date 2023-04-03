using Sudoku.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Z3.LinqBinding;

namespace Z3.LinqBinding.Sudoku
{

	/// <summary>
	/// Class that represents a Sudoku, fully or partially completed.
	/// It holds a list of 81 int for cells, with 0 for empty cells
	/// Can parse strings and files from most common formats and displays the sudoku in an easy to read format
	/// </summary>
	public class SudokuAsArray
	{

		private static readonly int[] Indices = Enumerable.Range(0, 9).ToArray();

		// The List property makes it easier to manipulate cells,
		public List<int> Cells { get; set; } = Enumerable.Repeat(0, 81).ToList();

		/// <summary>
		/// Creates a Z3 theorem to solve the sudoku, adding the general constraints, and the mask constraints for this particular Sudoku
		/// </summary>
		/// <param name="context">The linq to Z3 context wrapping Z3</param>
		/// <returns>a theorem with all constraints compounded</returns>
		public Theorem<SudokuAsArray> CreateTheorem(Z3Context context)
		{
			var toReturn = Create(context);
			for (int i = 0; i < 81; i++)
			{
				if (Cells[i] != 0)
				{
					var idx = i;
					var cellValue = Cells[i];
					toReturn = toReturn.Where(sudoku => sudoku.Cells[idx] == cellValue);
				}
			}

			return toReturn;

		}


		/// <summary>
		/// Creates a Z3-capable theorem to solve a Sudoku
		/// </summary>
		/// <param name="context">The wrapping Z3 context used to interpret c# Lambda into Z3 constraints</param>
		/// <returns>A typed theorem to be further filtered with additional contraints</returns>
		public static Theorem<SudokuAsArray> Create(Z3Context context)
		{

			var sudokuTheorem = context.NewTheorem<SudokuAsArray>();

			// Cells have values between 1 and 9
			for (int i = 0; i < 9; i++)
			{
				for (int j = 0; j < 9; j++)
				{
					//To avoid side effects with lambdas, we copy indices to local variables
					var i1 = i;
					var j1 = j;
					sudokuTheorem = sudokuTheorem.Where(sudoku => (sudoku.Cells[i1 * 9 + j1] > 0 && sudoku.Cells[i1 * 9 + j1] < 10));
				}
			}

			// Rows must have distinct digits
			for (int r = 0; r < 9; r++)
			{
				//Again we avoid Lambda closure side effects
				var r1 = r;
				sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(Indices.Select(j => t.Cells[r1 * 9 + j]).ToArray()));

			}

			// Columns must have distinct digits
			for (int c = 0; c < 9; c++)
			{
				//Preventing closure side effects
				var c1 = c;
				sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(Indices.Select(i => t.Cells[i * 9 + c1]).ToArray()));
			}

			// Boxes must have distinct digits
			for (int b = 0; b < 9; b++)
			{
				//On évite les effets de bords par closure
				var b1 = b;
				// We retrieve to top left cell for all boxes, using integer division and remainders.
				var iStart = b1 / 3;
				var jStart = b1 % 3;
				var indexStart = iStart * 3 * 9 + jStart * 3;
				sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(new int[]
					  {
					 t.Cells[indexStart ],
					 t.Cells[indexStart+1],
					 t.Cells[indexStart+2],
					 t.Cells[indexStart+9],
					 t.Cells[indexStart+10],
					 t.Cells[indexStart+11],
					 t.Cells[indexStart+18],
					 t.Cells[indexStart+19],
					 t.Cells[indexStart+20],
					  }
				   )
				);
			}

			return sudokuTheorem;
		}


		/// <summary>
		/// Displays a Sudoku in an easy-to-read format
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			var lineSep = new string('-', 31);
			var blankSep = new string(' ', 8);

			var output = new StringBuilder();
			output.Append(lineSep);
			output.AppendLine();

			for (int row = 1; row <= 9; row++)
			{
				output.Append("| ");
				for (int column = 1; column <= 9; column++)
				{

					var value = Cells[(row - 1) * 9 + (column - 1)];

					output.Append(value);
					if (column % 3 == 0)
					{
						output.Append(" | ");
					}
					else
					{
						output.Append("  ");
					}
				}

				output.AppendLine();
				if (row % 3 == 0)
				{
					output.Append(lineSep);
				}
				else
				{
					output.Append("| ");
					for (int i = 0; i < 3; i++)
					{
						output.Append(blankSep);
						output.Append("| ");
					}
				}
				output.AppendLine();
			}

			return output.ToString();
		}

		/// <summary>
		/// Parses a single Sudoku
		/// </summary>
		/// <param name="sudokuAsString">the string representing the sudoku</param>
		/// <returns>the parsed sudoku</returns>
		public static SudokuAsArray Parse(string sudokuAsString)
		{
			return ParseMulti(new[] { sudokuAsString })[0];
		}

		/// <summary>
		/// Parses a file with one or several sudokus
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns>the list of parsed Sudokus</returns>
		public static List<SudokuAsArray> ParseFile(string fileName)
		{
			return ParseMulti(File.ReadAllLines(fileName));
		}

		/// <summary>
		/// Parses a list of lines into a list of sudoku, accounting for most cases usually encountered
		/// </summary>
		/// <param name="lines">the lines of string to parse</param>
		/// <returns>the list of parsed Sudokus</returns>
		public static List<SudokuAsArray> ParseMulti(string[] lines)
		{
			var toReturn = new List<SudokuAsArray>();
			var cells = new List<int>(81);
			Console.WriteLine("debut");
			Console.WriteLine(lines.Length);
			Console.WriteLine("fin");
			foreach (var line in lines)
			{
				if (line.Length > 0)
				{
					if (char.IsDigit(line[0]) || line[0] == '.' || line[0] == 'X' || line[0] == '-')
					{
						foreach (char c in line)
						{
							int? cellToAdd = null;
							if (char.IsDigit(c))
							{
								var cell = (int)Char.GetNumericValue(c);
								cellToAdd = cell;
							}
							else
							{
								if (c == '.' || c == 'X' || c == '-')
								{
									cellToAdd = 0;
								}
							}

							if (cellToAdd.HasValue)
							{
								cells.Add(cellToAdd.Value);
								if (cells.Count == 81)
								{
									toReturn.Add(new SudokuAsArray() { Cells = new List<int>(cells) });
									cells.Clear();
								}
							}
						}
					}
				}
			}

			return toReturn;
		}

		public static SudokuAsArray ParseGrid(SudokuGrid s)
		{
			var cells = new List<int>(81);
			for (int row = 0; row < 9; row++)
			{
				for (int col = 0; col < 9; col++)
				{
					cells.Add(s.Cells[row][col]);
				}
			}
			var toReturn = new SudokuAsArray() { Cells = new List<int>(cells) };
			cells.Clear();
			return toReturn;
		}
		public string export()
		{
			var output = new StringBuilder();

			for (int row = 1; row <= 9; row++)
			{
				for (int column = 1; column <= 9; column++)
				{
					var value = Cells[(row - 1) * 9 + (column - 1)];
					output.Append(value);
				}
			}

			return output.ToString();
		}
	}
}
namespace LinqToZ3
{
	public class LinqFirstSolver : Sudoku.Shared.ISudokuSolver
	{
		public SudokuGrid Solve(SudokuGrid s)
		{
			var context = new Z3.LinqBinding.Z3Context();
			Z3.LinqBinding.Sudoku.SudokuAsArray.Create(context);
			var grid = Z3.LinqBinding.Sudoku.SudokuAsArray.ParseGrid(s);
			var theorem = grid.CreateTheorem(context);
			var sudokuSolved = theorem.Solve();
			var toReturn = SudokuGrid.ReadSudoku(sudokuSolved.export());
			return toReturn;
		}
	}

	// Je vois que vous avez utilisé la classe de Sudoku originale avec tableau de 81 cellules,
	// c'est un peu dommage car idéalement en veut travailler directement sur la classe SudokuGrid sans passer par une conversion.
	// En même temps vous n'avez pas complètement tord parcequ'il se trouve que la version originale de LinqBinding ne supportait pas les tableaux imbriqués comme me l'ont fait remarqué les derniers élèves qui ont choisi votre sujet.
	// Je me suis fait une grosse suée pour corriger ça (je vous laisse faire un diff si vous êtes curieux)
	// Du coup je ne peux m'empêcher de vous mettre le solver résultant, dont vous conviendrez qu'il est quand même plus clean. Je rajoute des compléments plus bas




	//public class Z3LinqSolver : ISudokuSolver
	//{

	//	private static readonly int[] Indices = Enumerable.Range(0, 9).ToArray();

	//	public SudokuGrid Solve(SudokuGrid s)
	//	{
	//		//Console.WriteLine("Le solver Z3-Linq a bien été appelé !");
	//		{
	//			using (var ctx = new Z3Context())
	//			{
	//				var theorem = CreateTheorem(ctx, s);
	//				s = theorem.Solve();
	//			}
	//		}
	//		return s;
	//	}

	//	public static Theorem<SudokuGrid> CreateTheorem(Z3Context context, SudokuGrid s)
	//	{
	//		var toReturn = Create(context);
	//		for (int i = 0; i < 9; i++)
	//		{
	//			for (int j = 0; j < 9; j++)
	//			{
	//				if (s.Cells[i][j] != 0)
	//				{
	//					var idxi = i;
	//					var idxj = j;
	//					var cellValue = s.Cells[i][j];
	//					toReturn = toReturn.Where(sudoku => sudoku.Cells[idxi][idxj] == cellValue);
	//				}
	//			}
	//		}
	//		return toReturn;
	//	}

	//	public static Theorem<SudokuGrid> Create(Z3Context context)
	//	{
	//		var sudokuTheorem = context.NewTheorem<SudokuGrid>();

	//		for (int i = 0; i < 9; i++)
	//		{
	//			for (int j = 0; j < 9; j++)
	//			{
	//				// To avoid closure side effects with lambdas, we copy indices to local variables
	//				var i1 = i;
	//				var j1 = j;
	//				sudokuTheorem = sudokuTheorem.Where(sudoku => (sudoku.Cells[i1][j1] >= 1) && (sudoku.Cells[i1][j1] <= 9));
	//			}
	//		}

	//		// Rows must have distinct digits
	//		for (int r = 0; r < 9; r++)
	//		{
	//			// Again we avoid Lambda closure side effects
	//			var r1 = r;
	//			//sudokuTheorem = sudokuTheorem.Where(t =>
	//			//    Z3Methods.Distinct(t.Cells[r1].Select((cell, j) => t.Cells[r1][j]).ToArray()));
	//			sudokuTheorem = sudokuTheorem.Where(t =>
	//				Z3Methods.Distinct(Indices.Select(j => t.Cells[r1][j]).ToArray()));
	//		}

	//		// Columns must have distinct digits
	//		for (int c = 0; c < 9; c++)
	//		{
	//			// Again we avoid Lambda closure side effects
	//			var c1 = c;
	//			//sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(t.Cells.Select((row, i) => t.Cells[i][c1]).ToArray()));
	//			sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(Indices.Select(i => t.Cells[i][c1]).ToArray()));
	//		}

	//		// Boxes must have distinct digits
	//		for (int b = 0; b < 9; b++)
	//		{
	//			//On Ã©vite les effets de bords par closure
	//			var b1 = b;
	//			// We retrieve to top left cell for all boxes, using integer division and remainders.
	//			var iStart = b1 / 3 * 3;
	//			var jStart = b1 % 3 * 3;
	//			//var indexStart = iStart * 3 * 9 + jStart * 3;
	//			sudokuTheorem = sudokuTheorem.Where(t => Z3Methods.Distinct(new int[]
	//					{
	//						t.Cells[iStart][jStart],
	//						t.Cells[iStart][jStart+1],
	//						t.Cells[iStart][jStart+2],
	//						t.Cells[iStart+1][jStart],
	//						t.Cells[iStart+1][jStart+1],
	//						t.Cells[iStart+1][jStart+2],
	//						t.Cells[iStart+2][jStart],
	//						t.Cells[iStart+2][jStart+1],
	//						t.Cells[iStart+2][jStart+2],
	//					}
	//				)
	//			);
	//		}
	//		return sudokuTheorem;
	//	}
	//}



	//Après, si vous êtes chaud pour aller plus loin, vous aurez sans doute remarqué que dans la version originale, il y avait comme ici 2 méthodes CreateTheorem, une statique qui ne dépend pas du sudoku à résoudre (contraintes génériques), et une d'instance qui rajoute les contraintes du masque (ici elles sont toutes les 2 statiques mais 1 seules prend le Sudoku en paramètre).
	// Ces contraintes sont du type toReturn = toReturn.Where(sudoku => sudoku.Cells[idxi][idxj] == cellValue) ce qui donne en syntaxe z3 pur quelque chose du type
	//instance_c = z3Context.MkAnd(instance_c, (BoolExpr)z3Context.MkEq(X[i][j], z3Context.MkInt(instance.Cells[i][j])))
	//Or on pourrait remarquer que c'est un peu con de définir des variables pour les cellules du masque, en fait ce sont des constantes, et on pourrait remplacer les variables en question par des constantes dans toutes les contraintes génériques qui les utilisent
	// Et bien il se trouve qu'il y a une API z3 qui fait justement ça.
	// En syntaxe z3 ça donne quelque chose du genre:


	//var substExprs = new List<Expr>();
	//var substVals = new List<Expr>();

	//for (int i = 0; i< 9; i++)
	//for (int j = 0; j< 9; j++)
	//	if (instance.Cells[i][j] != 0)
	//	{
	//		substExprs.Add(X[i][j]);
	//		substVals.Add(z3Context.MkInt(instance.Cells[i][j]));
	//	}

	//BoolExpr instance_c = (BoolExpr)GenericContraints.Substitute(substExprs.ToArray(), substVals.ToArray());

	//Il me semble qu'il n'y aurait pas grand chose à rajouter à linBinding pour supporter ça:
	// A priori ça se passe ici: https://github.com/meliioko/2023-EPITA-SCIA-PPC-Sudoku-CV/blob/main/Z3.LinqBinding/Theorem.cs#L833
	// et/ou ici: https://github.com/meliioko/2023-EPITA-SCIA-PPC-Sudoku-CV/blob/main/Z3.LinqBinding/Theorem.cs#L416
	// On doit pouvoir intercepter les cas où on se retrouve avec une expression z3 du type une variable = une constante
	// Dans ce cas là, pas la peine de rajouter le Assert: on met ça de côté et à la fin, on fait la substitution de la variable par la constante
	// Idéalement vous en faite une option histoire de pouvoir décliner 2 solvers et comparer les performances




}