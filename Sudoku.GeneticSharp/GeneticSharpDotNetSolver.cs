using GeneticSharp.Extensions;
using Sudoku.Shared;

namespace Sudoku.GeneticSharp
{
    public class GeneticSharpDotNetSolver : ISudokuSolver
    {
        private SudokuBoard GridToBoard(SudokuGrid s)
        {
            List<int> sdkList = new List<int>();
            for (int i = 0; i < s.Cells.Length; i++)
            {
                for (int j = 0; j < s.Cells[0].Length; j++)
                {
                    sdkList.Add(s.Cells[i][j]);
                }
            }

            return new SudokuBoard(sdkList);
        }
        
        
        private SudokuGrid BoardToGrid(SudokuBoard s)
        {
            SudokuGrid sdkGrid = new SudokuGrid();
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    sdkGrid.Cells[i][j] = s.GetCell(i, j);
                }
            }
            return sdkGrid;
        }
        
        public SudokuGrid Solve(SudokuGrid s)
        {
            SudokuBoard sdkB = GridToBoard(s);
            SudokuChromosome chromosome = new SudokuChromosome(sdkB);
            // var chromosome = new SudokuCellsChromosome(sdkB);
            // var chromosome = new SudokuPermutationsChromosome(sdkB);
            var sdkBoard = SudokuTestHelper.Eval(chromosome, sdkB, 5000, 0, 200);

            return BoardToGrid(sdkBoard);
        }
    }
}