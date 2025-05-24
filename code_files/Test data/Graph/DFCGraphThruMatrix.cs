using System;

class Program
{
    static void Main()
    {
        int[,] graph = {
            { 0, 1, 0, 0, 1 },
            { 1, 0, 1, 0, 1 },
            { 0, 1, 0, 1, 0 },
            { 0, 0, 1, 0, 1 },
            { 1, 1, 0, 1, 0 }
        };

        bool[] visited = new bool[graph.GetLength(0)];

        Console.WriteLine("DFS от вершины 0:");
        DFS(graph, 0, visited);
    }

    static void DFS(int[,] graph, int node, bool[] visited)
    {
        visited[node] = true;
        Console.WriteLine($"Вершина {node}");

        for (int i = 0; i < graph.GetLength(1); i++)
        {
            if (graph[node, i] == 1 && !visited[i])
            {
                DFS(graph, i, visited);
            }
        }
    }
}
