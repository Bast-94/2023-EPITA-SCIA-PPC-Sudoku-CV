using System;
using System.Collections.Generic;
using Sudoku.Shared;

namespace Sudoku.ParticleSwarmOptimization
{
    public class Worker : Organism
    {
        public override void SearchBetterSolution()
        {
            // FIXME: If is 'worker' organism then: Evolve or Mutate
            var new_solution = solution.CloneSudoku();
            
            Random r = new Random();

            bool swap = false;
            do
            {
                int row = r.Next(0, SudokuGrid.NeighbourIndices.Count);
                var possibleSwap = new List<bool>() {false, false, false, false, false, false, false, false, false};
                foreach (var colIndex in SudokuGrid.NeighbourIndices)
                    possibleSwap[colIndex] = PSOSolver.original.Cells[row][colIndex] == 0;

                if (possibleSwap.FindAll(e => e).Count < 2)
                    continue;
                
                int col_1;
                int col_2;
                do
                {
                    col_1 = r.Next(0, SudokuGrid.NeighbourIndices.Count);
                    col_2 = r.Next(0, SudokuGrid.NeighbourIndices.Count);
                } while (col_1 == col_2 || !possibleSwap[col_1] || !possibleSwap[col_2]);

                var tmp = new_solution.Cells[row][col_1];
                new_solution.Cells[row][col_1] = new_solution.Cells[row][col_2];
                new_solution.Cells[row][col_2] = tmp;
                swap = true;
            } while (!swap);

            var new_error = new_solution.NbErrors(PSOSolver.original);

            if (new_error < error)
            {
                //Console.WriteLine("New error:" + error + " -> " + new_error);
                error = new_error;
                solution = new_solution;
            }
            else
            {
                age++;
                if (age > 1000)
                {
                    solution = SudokuUtils.RandomSolution();
                    error = solution.NbErrors(PSOSolver.original);
                    age = 0;
                }
            }
        }

        // Replace this Worker by merging worker and explorer
        public void Replace(Worker worker, Explorer explorer)
        {
            var rd = new Random();
            solution = worker.solution.CloneSudoku();
            
            foreach (var rowIndex in SudokuGrid.NeighbourIndices)
                foreach (var colIndex in SudokuGrid.NeighbourIndices)
                    // Not in solution
                    if (PSOSolver.original.Cells[rowIndex][colIndex]>0 && PSOSolver.original.Cells[rowIndex][colIndex] != solution.Cells[rowIndex][colIndex])
                        if (rd.Next(0, 100) < 50)
                            solution.Cells[rowIndex][colIndex] = explorer.Solution.Cells[rowIndex][colIndex];
        }
    }
}