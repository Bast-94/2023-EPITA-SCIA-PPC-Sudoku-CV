using System;
using System.Collections.Generic;
using System.Linq;
using GeneticSharp;
using GeneticSharp.Extensions;

namespace Sudoku.GeneticSharp
{
    public class SudokuChromosome : MyChromosomeBase, ISudokuChromosome
    {
        public SudokuChromosome()
            : this((SudokuBoard)null)
        {
        }

        public SudokuChromosome(SudokuBoard targetSudokuBoard)
            : this(targetSudokuBoard, (Dictionary<int, List<int>>)null)
        {
        }

        public SudokuChromosome(
            SudokuBoard targetSudokuBoard,
            Dictionary<int, List<int>> extendedMask)
            : base(targetSudokuBoard, extendedMask, 81)
        {
        }

        public static List<int> moduloTable = new List<int>(82)
        {
            0,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9
        };

        public override Gene GenerateGene(int geneIndex)
        {
            int value = -1;
            if (this.TargetSudokuBoard != null && this.TargetSudokuBoard.Cells[geneIndex] != 0)
            {
                value = this.TargetSudokuBoard.Cells[geneIndex] +
                        this.cloneLookupTable[this.TargetSudokuBoard.Cells[geneIndex] - 1] * 9;
                this.cloneLookupTable[this.TargetSudokuBoard.Cells[geneIndex] - 1]++;
            }

            if (value == -1)
            {
                for (int i = 0; i < this.ExtendedMask[geneIndex].Count; i++)
                {
                    int lookup = this.ExtendedMask[geneIndex][i] - 1;
                    if (this.lookupTable[lookup] < 9)
                    {
                        value = this.ExtendedMask[geneIndex][i] + this.lookupTable[lookup] * 9;
                        this.lookupTable[lookup]++;
                        break;
                    }
                }
            }

            Random random = new Random();
            if (value == -1)
            {
                // Loop over 0 to lookupTable.Count randomly
                List<int> randomIndexes = Enumerable.Range(0, this.lookupTable.Count).ToList();
                randomIndexes = randomIndexes.OrderBy(i => random.Next()).ToList();
                foreach (int i in randomIndexes)
                {
                    if (this.lookupTable[i] < 9)
                    {
                        value = (i + 1) + this.lookupTable[i] * 9;
                        this.lookupTable[i]++;
                        break;
                    }
                }
            }
            
            if (value == -1)
            {
                value = (int)GetGenes()[geneIndex].Value;
            }
           
            Gene gene = new Gene((object)value);
            return gene;
        }

        public override IChromosome CreateNew() =>
            (IChromosome)new SudokuChromosome(this.TargetSudokuBoard, this.ExtendedMask);

        public override IList<SudokuBoard> GetSudokus() => (IList<SudokuBoard>)new List<SudokuBoard>(
            (IEnumerable<SudokuBoard>)new SudokuBoard[1]
            {
                new SudokuBoard(
                    ((IEnumerable<Gene>)this.GetGenes()).Select<Gene, int>((Func<Gene, int>)(g => moduloTable[(int)g.Value])))
            });
    }
}