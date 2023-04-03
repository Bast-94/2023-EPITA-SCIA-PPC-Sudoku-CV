import numpy as np
from pycsp3 import *

instance = ((0,0,0,0,9,4,0,3,0),
        (0,0,0,5,1,0,0,0,7),
        (0,8,9,0,0,0,0,4,0),
        (0,0,0,0,0,0,2,0,8),
        (0,6,0,2,0,1,0,5,0),
        (1,0,2,0,0,0,0,0,0),
        (0,7,0,0,0,0,5,2,0),
        (9,0,0,0,6,5,0,0,0),
        (0,4,0,9,7,0,0,0,0))

# clues = data  # if not 0, clues[i][j] is a value imposed at row i and col j
clues = instance

# x[i][j] is the value at row i and col j
grid = VarArray(size=[9, 9], dom=range(1, 10))

satisfy(
   # imposing distinct values on each row and each column
   AllDifferent(grid, matrix=True),

   # imposing distinct values on each block  tag(blocks)
   [AllDifferent(grid[i:i + 3, j:j + 3]) for i in [0, 3, 6] for j in [0, 3, 6]],

   # imposing clues  tag(clues)
   [grid[i][j] == clues[i][j] for i in range(9) for j in range(9)
     if clues and clues[i][j] > 0]
)

r = []

if solve() is SAT:
    r = list(values(grid))
    
print(r)