using System.Linq;
using Sudoku.Shared;

namespace Sudoku.ColorGraph
{
    
    /**
     * A vertex of a colored graph
     */
    public class Vertex
    {
        public readonly int Id;     // Id of the vertex (0-80)
        public int Color;           // 0 = uncolored, 1-9 = colored
        public Vertex(int id, int color)
        {
            Id = id;
            Color = color;
        }
       
    }
    
    /**
     * A *colored graph* representing a SudokuGrid
     */
    public class CGraph
    {
        private readonly List<List<Vertex>> _adjacencyList;
        public readonly List<Vertex> Nodes;
        
        /**
         * Create a graph from a SudokuGrid
         * @param s
         */
        public CGraph(SudokuGrid s)
        {
            // Add all vertices to the graph (81 vertices)
            Nodes = new List<Vertex>();
            for (int i = 0; i < 9 * 9 ; i++)
                Nodes.Add(new Vertex(i, 0));
            
            // Link vertices to their neighbors
            _adjacencyList = new List<List<Vertex>>();
            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    HashSet<Vertex> subList = new HashSet<Vertex>();
                    for (int cell = 0; cell < 9; cell++)
                    {
                        // Cells in the same row
                        if (cell != j)
                        {
                            Nodes[i * 9 + cell].Color = s.Cells[i][cell];
                            subList.Add(Nodes[i * 9 + cell]);
                        }
                        
                        // Cells in the same column
                        if (cell != i)
                        {
                            Nodes[cell * 9 + j].Color = s.Cells[cell][j];
                            subList.Add(Nodes[cell * 9 + j]);
                        }
                    }
                    
                    // Cells in the same block
                    int iBlock = i / 3;
                    int jBlock = j / 3;
                    for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 3; b++)
                        if (!(iBlock * 3 + a == i && jBlock * 3 + b == j))
                        {
                            Nodes[(iBlock * 3 + a) * 9 + jBlock * 3 + b].Color = s.Cells[iBlock * 3 + a][jBlock * 3 + b];
                            subList.Add(Nodes[(iBlock * 3 + a) * 9 + jBlock * 3 + b]);
                        }

                    _adjacencyList.Add(subList.ToList());
                }
            }
        }
        
        /**
         * Get the neighbors of a vertex
         * @param g
         * @param p
         * @return List<Vertex>
         */
        public List<Vertex> GetNeighbors(Vertex p)
        {
            return _adjacencyList[p.Id];
        }
        
        /**
         * Get all uncolored vertices of the graph
         * @param g
         * @return HashSet<Vertex>
         */
        public HashSet<Vertex> GetUncoloredVertices()
        {
            var uncoloredVertices = new HashSet<Vertex>();
            foreach (var vertex in Nodes)
                if (vertex.Color == 0)
                    uncoloredVertices.Add(vertex);

            return uncoloredVertices;
        }
        
        /**
         * Get all possible colors for a vertex (not already used by its neighbors)
         * @param g
         * @param vertex
         * @return List<int>
         */
        public List<int> GetPossibleColors(Vertex vertex)
        {
            if (vertex.Color != 0)
                throw new Exception("This vertex is already colored");
            
            int color = 1;
            var neighbors = GetNeighbors(vertex);

            var possibleColors = new List<int>();
            while (color <= 9)
            {
                if (neighbors.All(neighbor => neighbor.Color != color))
                    possibleColors.Add(color);
                
                color++;
            }

            return possibleColors;
        }
        
        /**
         * Get the saturation of a vertex
         * @param g
         * @param vertex
         * @return int
         */
        private int GetSaturation(Vertex vertex)
        {
            var saturation = 0;
            foreach (var v in GetNeighbors(vertex))
                if (v.Color != 0)
                    saturation++;

            return saturation;
        }

        /**
         * Get the vertex with the highest saturation
         * @param g
         * @param vertices
         * @return Vertex
         */
        public Vertex? GetMostSaturatedVertex()
        {
            var vertices = GetUncoloredVertices();
            if (vertices.Count == 0)
                return null;
            
            var mostSaturatedVertex = vertices.First();
            var maxSaturation = -1;
            
            foreach (var vertex in vertices)
            {
                var saturation = GetSaturation(vertex);
                if (saturation > maxSaturation)
                {
                    maxSaturation = saturation;
                    mostSaturatedVertex = vertex;
                }
            }
            
            return mostSaturatedVertex;
        }
        
        /**
         * Convert the graph to a SudokuGrid
         * @return SudokuGrid
         */
        public SudokuGrid ToSudokuGrid()
        {
            var result = new SudokuGrid();
            for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                result.Cells[i][j] = Nodes[i * 9 + j].Color;
                
            return result;
        }
    }
}