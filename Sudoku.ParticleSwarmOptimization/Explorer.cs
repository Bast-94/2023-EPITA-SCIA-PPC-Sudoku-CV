using Sudoku.Shared;

namespace Sudoku.ParticleSwarmOptimization
{
    public class Explorer : Organism
    {
        public override void SearchBetterSolution()
        {
            // Explore
            solution = SudokuUtils.RandomSolution();
            error = solution.NbErrors(PSOSolver.original);
        }
    }
}