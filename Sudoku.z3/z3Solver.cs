using Sudoku.Shared;
using System;
using Microsoft.Z3;

namespace Sudoku.z3
{

	//Quelques suggestions: comme vous êtes partis d'un code similaire, je vous met un lien vers les commentaires que j'ai mis à vos camarades:
	// https://github.com/jsboigeEpita/2023-EPITA-SCIA-PPC-Sudoku-NLP/blob/8cf051bfcae9f6d2bf279c9f1896f88049470dcd/Sudoku.Z3Solver/Z3Solver.cs
	// Visiblement il serait intéressant de tester une implémentation en vecteurs de bits: https://sites.google.com/site/modante/programs_description
	//Et puis sinon il serait intéressant de regarder du côté des tactiques 
	// https://microsoft.github.io/z3guide/docs/strategies/intro
	//https://microsoft.github.io/z3guide/programming/Z3%20Python%20-%20Readonly/Strategies/
	//https://github.com/Z3Prover/z3/blob/master/examples/dotnet/Program.cs#L209



	public class z3Solver : ISudokuSolver
	{
		public z3Solver()
		{

		}
		public SudokuGrid Solve(SudokuGrid s)
		{

			using (Context ctx = new Context())
            {
				int[][] grid = s.Cells; // Faire un attribut de classe

                Solver solver = ctx.MkSolver(); // Faire un attribut de classe
                // Créer une variable pour chaque case de la grille
                // remplacer la double boucle la méthode init_first_constraints(Context ctx )
                IntExpr[,] vars = new IntExpr[9, 9];
                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        vars[i, j] = (IntExpr)ctx.MkIntConst($"{i},{j}");
                        solver.Add(ctx.MkAnd(ctx.MkGe(vars[i, j], ctx.MkInt(1)), ctx.MkLe(vars[i, j], ctx.MkInt(9))));
                    }
                }

                // Ajouter les contraintes de rangée, de colonne et de région
                // remplacer la double boucle la méthode init_all_constraints(Context ctx)
                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        // Contrainte de rangée
                        BoolExpr rowConstraint = ctx.MkTrue();
                        // remplacer la boucle la méthode private BoolExpr init_row_constraints(Context ctx, int i, int j)
                        for (int k = 0; k < 9; k++)
                        {
                            if (k != j)
                            {
                                var notEq =  ctx.MkNot(ctx.MkEq(vars[i, j], vars[i, k]));
                                rowConstraint = ctx.MkAnd(rowConstraint, notEq);
                            }
                        }

                        // Contrainte de colonne
                        // remplacer la boucle la méthode private BoolExpr init_col_constraints(Context ctx, int i, int j)
                        BoolExpr colConstraint = ctx.MkTrue();
                        for (int k = 0; k < 9; k++)
                        {
                            if (k != i)
                            {
                                colConstraint = ctx.MkAnd(colConstraint, ctx.MkNot(ctx.MkEq(vars[i, j], vars[k, j])));
                            }
                        }

                        // Contrainte de région
                        // remplacer la boucle la méthode private BoolExpr  get_region_constraints(Context ctx, int i, int j)
                        int rowRegion = (i / 3) * 3;
                        int colRegion = (j / 3) * 3;
                        BoolExpr regionConstraint = ctx.MkTrue();
                        for (int k = rowRegion; k < rowRegion + 3; k++)
                        {
                            for (int l = colRegion; l < colRegion + 3; l++)
                            {
                                if (k != i || l != j)
                                {
                                    regionConstraint = ctx.MkAnd(regionConstraint, ctx.MkNot(ctx.MkEq(vars[i, j], vars[k, l])));
                                }
                            }
                        } 
                    
                        // Ajouter les contraintes à la grille
                        // Remplacer les asserts par la méthode private void assert_constraints(Context ctx )
                        solver.Assert(rowConstraint);
                        solver.Assert(colConstraint);
                        solver.Assert(regionConstraint);

                        // Ajouter les valeurs connues de la grille
                        // Méthode private void assert_known_values (Context ctx , int i , int j)
                        if (grid[i][j] != 0)
                        {
                            solver.Assert(ctx.MkEq(vars[i, j], ctx.MkInt(grid[i][j])));
                        } 
                    }
                        
                }
                
                // Résoudre le sudoku
                // Faire une méthode pour renvoyer la résolution.
				SudokuGrid r = new SudokuGrid();
                if (solver.Check() == Status.SATISFIABLE)
                {
                    Model model = solver.Model;
                    for (int i = 0; i < 9; i++)
                    {
                        for (int j = 0; j < 9; j++)
                        {
							int value = (model.Evaluate(vars[i, j]) as IntNum).Int;
                            r.Cells[i][j] = value;
                        }
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("Le sudoku n'a pas de solution.");
                } 
				return r;
            }
		}
	}
}