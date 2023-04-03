using System;
using System.Collections.Generic;
using System.Linq;
using GeneticSharp;
using GeneticSharp.Extensions;

namespace Sudoku.GeneticSharp
{
  public abstract class MyChromosomeBase : ChromosomeBase, ISudokuChromosome
  {
    private readonly SudokuBoard _targetSudokuBoard;
    private Dictionary<int, List<int>> _extendedMask;
    public IList<int> lookupTable;
    public IList<int> cloneLookupTable;
    
    protected MyChromosomeBase(
      SudokuBoard targetSudokuBoard,
      Dictionary<int, List<int>> extendedMask,
      int length)
      : base(length)
    {
      this._targetSudokuBoard = targetSudokuBoard;
      this._extendedMask = extendedMask;
      this.lookupTable = (IList<int>)new List<int>(9) { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
      // Add 1 to the lookup table for each number that is already in the board
      for (int i = 0; i < this._targetSudokuBoard.Cells.Count; i++)
      {
          if (this._targetSudokuBoard.Cells[i] != 0)
          {
            this.lookupTable[this._targetSudokuBoard.Cells[i] - 1]++;
          }
      }
      this.cloneLookupTable = (IList<int>)new List<int>(9) { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
      this.CreateGenes();
    }

    public SudokuBoard TargetSudokuBoard => this._targetSudokuBoard;

    public Dictionary<int, List<int>> ExtendedMask
    {
      get
      {
        if (this._extendedMask == null)
          this.BuildExtenedMask();
        return this._extendedMask;
      }
    }

    private void BuildExtenedMask()
    {
      List<int> list = Enumerable.Range(1, 9).ToList<int>();
      Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>(81);
      if (this._targetSudokuBoard != null)
      {
        Dictionary<int, List<int>> forbiddenMask = new Dictionary<int, List<int>>();
        List<int> intList = (List<int>) null;
        for (int index1 = 0; index1 < this._targetSudokuBoard.Cells.Count; ++index1)
        {
          int cell = this._targetSudokuBoard.Cells[index1];
          if (cell != 0)
          {
            int num1 = index1 / 9;
            int num2 = index1 % 9;
            int num3 = index1 / 27 * 27 + index1 % 9 / 3 * 3;
            for (int index2 = 0; index2 < 9; ++index2)
            {
              int num4 = num3 + index2 % 3 + index2 / 3 * 9;
              int[] numArray = new int[3]
              {
                num1 * 9 + index2,
                index2 * 9 + num2,
                num4
              };
              foreach (int key in numArray)
              {
                if (key != index1)
                {
                  if (!forbiddenMask.TryGetValue(key, out intList))
                  {
                    intList = new List<int>();
                    forbiddenMask[key] = intList;
                  }
                  if (!intList.Contains(cell))
                    intList.Add(cell);
                }
              }
            }
          }
        }
        for (int index = 0; index < this._targetSudokuBoard.Cells.Count; index++)
          dictionary[index] = list.Where<int>((Func<int, bool>) (i => !forbiddenMask[index].Contains(i))).ToList<int>();
      }
      else
      {
        for (int key = 0; key < 81; ++key)
          dictionary.Add(key, list);
      }
      this._extendedMask = dictionary;
    }

    public abstract IList<SudokuBoard> GetSudokus();
  }

}