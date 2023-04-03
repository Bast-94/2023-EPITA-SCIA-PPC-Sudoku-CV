using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sudoku.Shared;

namespace Sudoku.ParticleSwarmOptimization
{
    public class Hive
    {
        private Worker[] workers;
        private Explorer[] explorers;

        private Worker bestWorker;
        private Explorer bestExplorer;
        private Organism bestOrganism;

        public Organism BestOrganism => bestOrganism;

        public Hive(uint nb_explorer, uint nb_worker)
        {
            workers = new Worker[nb_worker];
            explorers = new Explorer[nb_explorer];
            
            Parallel.For(0, nb_worker, i => { workers[i] = new Worker(); });
            Parallel.For(0, nb_explorer, i => { explorers[i] = new Explorer(); });
            UpdateBestOrganism();
        }

        private void UpdateBestOrganism()
        {
            bestWorker = workers.Min();
            bestExplorer = explorers.Min();
            // Console.WriteLine("Best Worker: " + bestWorker.Error + " Best Explorer:" + bestExplorer.Error);
            if (bestOrganism == null || bestWorker.Error < bestOrganism.Error)
                bestOrganism = bestWorker;
            if (bestOrganism == null || bestExplorer.Error < bestOrganism.Error)
                bestOrganism = bestExplorer;
        }

        public SudokuGrid Solve()
        {
            for (int i = 0; i < 100000; i++)
            {
                //Console.WriteLine("Epoch: " + i + " Error: " + bestOrganism.Error);
                if (bestOrganism.Error == 0)
                    break;
                Parallel.ForEach(explorers, organism => organism.SearchBetterSolution());
                Parallel.ForEach(workers, organism => organism.SearchBetterSolution());
                UpdateBestOrganism();
                workers.Max()?.Replace(bestWorker, bestExplorer);
            }
            return bestOrganism.Solution;
        }
    }
}