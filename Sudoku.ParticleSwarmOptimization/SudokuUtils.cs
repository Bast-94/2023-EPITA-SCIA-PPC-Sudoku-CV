using System;
using System.Collections.Generic;
using Sudoku.Shared;

namespace Sudoku.ParticleSwarmOptimization
{
    public static class SudokuUtils
    {
        // Fill the Sudoku grid with random values but respecting line constraint
        public static SudokuGrid RandomSolution()
        {
            var solution = PSOSolver.original.CloneSudoku();
            Random r = new Random();
            foreach (var rowIndex in SudokuGrid.NeighbourIndices)
            {
                // Get possible numbers in a column
                var possibleNumbers = new List<int>() {1, 2, 3, 4, 5, 6, 7, 8, 9};
                foreach (var colIndex in SudokuGrid.NeighbourIndices)
                    if (solution.Cells[rowIndex][colIndex] != 0)
                        possibleNumbers.Remove(solution.Cells[rowIndex][colIndex]);

                // Place possible numbers
                foreach (var colIndex in SudokuGrid.NeighbourIndices)
                {
                    if (solution.Cells[rowIndex][colIndex] != 0)
                        continue;

                    var randomIndex = r.Next(0, possibleNumbers.Count);
                    solution.Cells[rowIndex][colIndex] = possibleNumbers[randomIndex];
                    possibleNumbers.RemoveAt(randomIndex);
                }
            }

            return solution;
        }
    }
}