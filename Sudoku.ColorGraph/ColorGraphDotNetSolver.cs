using Sudoku.Shared;

namespace Sudoku.ColorGraph
{
    public class ColorGraphDotNetSolver : ISudokuSolver
    {
        private int _backtracks;
        
        /**
         * Solve a SudokuGrid using graph coloring algorithms
         * @param s
         * @return the solved SudokuGrid
         */
        public SudokuGrid Solve(SudokuGrid s)
        {
            CGraph graph = new CGraph(s);
            DSatur(graph);
            
            Console.WriteLine($"Backtracks: {_backtracks}");
            
            return graph.ToSudokuGrid();
        }
        
        
        
        /**
         * Recursive function to color the graph using the DSatur algorithm
         * @param g
         * @return true if the graph is colored, false if the graph is not colorable
         */
        private bool DSatur(CGraph g)
        {
            var uncoloredVertices = g.GetUncoloredVertices();
            if (uncoloredVertices.Count == 0)
                return true;
            
            Vertex? vertex = g.GetMostSaturatedVertex();
            if (vertex == null)
                return true;

            foreach (var color in g.GetPossibleColors(vertex))
            {
                vertex.Color = color;
                
                if (DSatur(g))
                    return true;
                
                _backtracks++;
                vertex.Color = 0;
            }

            return false;
        }
    }
}

