# Pathfinding Visualiser

A Windows Forms app (C#) that makes a map, either hilly terrain or a maze, and shows six search algorithms finding a way across it one cell at a time.

![The app comparing all six algorithms on a terrain map](docs/window.png)

## Where it came from

This started as my A-level Computer Science NEA. That version (a separate repo, "NEA Grid") only got as far as the grid: a 20 × 20 board of `PictureBox`es with Draw and Erase buttons that coloured squares as the mouse went over them. There was a black border, two number boxes meant for the grid size, and that was it. It also had a bug I didn't spot at the time: `for (int lines = 0; lines < xDraw; lines=+2)` sets `lines` to 2 forever instead of adding 2, so it never finished drawing the grid.

I came back to it and finished it properly. The ideas are the same (a grid you draw walls on, a border round the edge, the size set from two boxes), but the grid is now drawn in one go instead of 400 separate controls, so it copes with 151 × 101 without slowing down. The searching, map generation and the rest of the UI are new.

## What it does

- **Makes maps.** Perlin-noise terrain where the height of each cell is how much it costs to walk over (1 on flat ground up to 9 on steep hills, with the highest points turned into rock you can't cross). Or a perfect maze from a recursive backtracker. Or an empty grid to draw on.
- **Lets you edit them.** Draw and erase walls, raise and lower the ground, move the start and end. Brush size 1 to 5.
- **Animates the search.** The blue area is cells that have been checked, the yellow edge is cells waiting to be checked, and the orange line is the path. You can pause, step one cell at a time, or change the speed.
- **Compares them.** "Run all 6" runs every algorithm on the same map and shows cells visited and path cost side by side. The orange bars are the ones that found the cheapest path.
- **Saves a picture** of the current map and search.

## The algorithms

| Algorithm | Cares about hills? | Always finds the cheapest path? | What it does |
|---|---|---|---|
| A* | Yes | Yes | Dijkstra plus a guess of the distance left, so it heads towards the end |
| Dijkstra | Yes | Yes | Always checks the cheapest cell so far next, spreading out evenly |
| Greedy best-first | No | No | Only uses the guess, so it's very fast but goes straight over hills |
| Breadth-first | No | Fewest steps, not cheapest | Every step counts as 1 |
| Bidirectional BFS | No | Fewest steps, not cheapest | Two breadth-first searches, one from each end, that stop when they meet |
| Depth-first | No | No | Goes as far as it can before backtracking. Here to show what *not* to do |

Diagonal moves can be turned off. When they're on, a diagonal step costs 1.41 times as much and can't squeeze between two walls that touch at a corner. The guess A* uses is the octile distance (the shortest possible route on an 8-way grid, ignoring walls and hills). Flat ground costs 1, so that guess is never more than the real cost, which is what keeps A* finding the cheapest path.

## Results

All six on the same 81 × 51 terrain (seed 11, diagonals on). "Cells visited" is how many cells each one had to check. "Path cost" adds up the hill cost of every cell on the path.

| Algorithm | Cells visited | Path steps | Path cost |
|---|---|---|---|
| A* | 1,665 | 78 | 111.7 |
| Dijkstra | 2,952 | 78 | 111.7 |
| Greedy best-first | 80 | 78 | 166.4 |
| Breadth-first | 3,508 | 78 | 161.9 |
| Bidirectional BFS | 3,439 | 78 | 164.9 |
| Depth-first | 3,481 | 3,210 | 13,767.7 |

- A* and Dijkstra find the same cheapest route, but A* only checks 56 % as many cells.
- Greedy is by far the quickest (80 cells), but its path costs about 50 % more, because it walks straight over the hills.
- Breadth-first finds a path with the same number of steps, but it also goes over the hills, so it costs 45 % more.
- Depth-first wanders round almost the whole map before it reaches the end.

In a maze every open cell costs the same and there's only one route, so they all find the same path. The difference is how much of the maze they explore first (seed 7: greedy 1,154 cells, A* 1,776, Dijkstra 1,791).

You can get these tables yourself with `PathfindingVisualiser --stats terrain 11`.

## Running it

Needs Windows and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet run --project PathfindingVisualiser -c Release
```

Or open `PathfindingVisualiser.sln` in Visual Studio and press F5.

| Do this | How |
|---|---|
| Use the selected draw tool | Left-click or drag on the grid |
| Erase | Right-click or drag |
| Run / pause | Run button or Space |
| One step | Step button or → |
| New terrain / new maze | T / M |
| Clear the search | C |
| Pick a draw tool | 1 to 6 |

Without opening the window:

```
PathfindingVisualiser --export out.png astar terrain 11     # astar, dijkstra, greedy, bfs, bds, dfs / terrain, maze / seed
PathfindingVisualiser --stats maze 7
```

## Tests

`tests/` checks the searches on 100 random maps (terrain and maze, diagonals on and off). It checks that:

- every path is valid,
- A* always matches Dijkstra's cost while checking no more cells,
- nothing beats Dijkstra on cost,
- nothing beats breadth-first on number of steps,
- a walled-off end gives "no path",
- diagonals can't cut corners.

It doesn't use the UI, so it runs on any OS:

```
dotnet run --project tests
```

## Files

```
PathfindingVisualiser/
  Grid/Map.cs                 the grid: cells, walls, hill costs, neighbours
  Generation/Generators.cs    Perlin noise, terrain and maze generators
  Search/Search.cs            all six algorithms (as step-by-step iterators) and a min-heap
  UI/GridView.cs              draws the map and the search, mouse drawing
  UI/MainForm.cs              the window, sidebar and comparison chart
  Program.cs                  starts the app, plus the --export and --stats options
tests/SearchTests.cs
```
