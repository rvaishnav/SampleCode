using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;

public class cellInfo : IComparable<cellInfo>
    {
       public Vector2 coord;
       public float f_score;
       public float g_score;
       public int obstacle;
       public cellInfo(int x = 0 , int y = 0)
       {
           coord.x = x;
           coord.y = y;
       }
       
       /// <summary>
       /// custom comparer for cell class
       /// </summary>
       /// <param name="b"></param>
       /// <returns></returns>
       public int CompareTo(cellInfo b)
       {
            if (this.f_score > b.f_score)
                return 1;
            if (this.f_score < b.f_score)
                return -1;
            else
                return 0;
       }
    }

public class AstarPath : MonoBehaviour 
{
    private const int infinity = 10000;
    private int mapWidth;
    private int mapHeight;
    private int[] dx = { 1, 1, 1, 0, 0, -1, -1, -1 };
    private int[] dy = { 0, 1, -1, 1, -1, 0, 1, -1, };
    private Dictionary<Vector2, cellInfo> graph;
    private Dictionary<Vector2, Vector2> cameFrom;
    private List<cellInfo> openSet;
    private List<cellInfo> closedSet;
    private List<Vector2> path;

    void Start()
    {
        mapHeight = 60;
        mapWidth = 60;
        
        graph = new Dictionary<Vector2, cellInfo>();
        openSet = new List<cellInfo>();
        closedSet = new List<cellInfo>();
        cameFrom = new Dictionary<Vector2, Vector2>();
        path = new List<Vector2>();
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                cellInfo cell = new cellInfo();
                cell.coord.x = i;
                cell.coord.y = j;
                cell.f_score = infinity;
                cell.g_score = infinity;
                graph.Add(cell.coord, cell);
            }
        }
        graph[new Vector2(3, 4)].obstacle = 1;
        graph[new Vector2(4, 3)].obstacle = 1;
        graph[new Vector2(4, 4)].obstacle = 1;

        //Astar(new Vector2(3, 3), new Vector2(5, 5));
        //constructPathString();
    }

    //------------------------------------------------------------------

    /// <summary>
    /// implements Astar algorithm as depicted on wikipedia
    /// </summary>
    /// <param name="start">start cell coordinate</param>
    /// <param name="goal">destination cell coordinate</param>
    public String Astar(Vector2 start, Vector2 goal)
    {
        closedSet.Clear();
        openSet.Clear();
        cameFrom.Clear();
		path.Clear();
        graph[start].g_score = 0;
        graph[start].f_score = graph[start].g_score + HeuristicCostEstimate(start, goal);
        openSet.Add(graph[start]);
        openSet.Sort();
        
        while (openSet.Count != 0)
        {
            if (openSet[0].coord == goal)
            {
                reconstructPath(start, goal);
                return constructPathString();
            }
            cellInfo current = openSet.FirstOrDefault();
            closedSet.Add(current);
            openSet.RemoveAt(0);
            openSet.Sort();
            //compute neighbors
            for (int i = 0; i < 8; i++)
            {
                Vector2 npos = new Vector2(current.coord.x+dx[i],current.coord.y+dy[i]);
                if (npos.x < 0 || npos.x >= mapWidth)
                {
                    continue;
                }
                if (npos.y < 0 || npos.y >= mapHeight)
                {
                    continue;
                }
                if (graph[npos].obstacle == 1)
                {
                    continue;
                }
                float tentative_gscore = current.g_score + DistanceBetween(current.coord,npos);
                float tentative_fscore = tentative_gscore + HeuristicCostEstimate(npos, goal);

                if (tentative_fscore > graph[npos].f_score && closedSet.Contains(graph[npos]))
                {
                    continue;
                }
                if (tentative_fscore < graph[npos].f_score || !openSet.Contains(graph[npos]))
                {
                    graph[npos].f_score = tentative_fscore;
                    graph[npos].g_score = tentative_gscore;
                    if (cameFrom.ContainsKey(npos))
                    {
                        cameFrom[npos] = current.coord;
                    }
                    else
                    {
                        cameFrom.Add(npos, current.coord);
                    }
                    if (!openSet.Contains(graph[npos]))
                    {
                        openSet.Add(graph[npos]);
                        openSet.Sort();
                    }
                }
            }
        }
        return "";
    }

    //------------------------------------------------------------------

    /// <summary>
    /// straight line distance
    /// </summary>
    /// <returns></returns>
    public float HeuristicCostEstimate(Vector2 start, Vector2 goal)
    {
        return Vector2.Distance(goal, start);
    }

    //------------------------------------------------------------------
    /// <summary>
    /// Distance between 2 cells
    /// </summary>
    /// <returns></returns>
    public float DistanceBetween(Vector2 current, Vector2 neighbour)
    {
        return Vector2.Distance(current, neighbour);
    }

    //------------------------------------------------------------------
    
    /// <summary>
    /// reconstructing path by backtracking
    /// </summary>
    public void reconstructPath(Vector2 start ,Vector2 goal)
    {
        path.Add(goal);
        while(goal!=start)
        {
            Vector2 pre = cameFrom[goal];
            path.Add(pre);
            goal = pre;
        }
    }

    //------------------------------------------------------------------
    /// <summary>
    /// construct a string for the entire path
    /// </summary>
    String constructPathString()
    {
        String steps = "";
        for (int i = path.Count-1; i > 0; i--)
        {
            Vector2 hop = path[i-1] - path[i];
            if (hop.y == 0 && hop.x == 1)
            {
				steps+=0;
            }
            if (hop.y == 0 && hop.x == -1)
            {
				steps+=2;
            }
            if (hop.y == 1 && hop.x == 1)
            {
				steps+=4;
            }
            if (hop.y == 1 && hop.x == 0)
            {
				steps+=1;
            }
            if (hop.y == 1 && hop.x == -1)
            {
				steps+=5;
            }
            if (hop.y == -1 && hop.x == 1)
            {
				steps+=7;
            }
            if (hop.y == -1 && hop.x == 0)
            {
				steps+=3;
            }
            if (hop.y == -1 && hop.x == -1)
            {
				steps+=6;
            }
        }
        return steps.ToString();
    }

    //------------------------------------------------------------------
    
    /// <summary>
    /// sets the obstacles in the cells of map
    /// 0=clear 1 = obstacle
    /// </summary>
    /// <param name="obstacles"></param>
   	public  void setobstables(int[,] obstacles)
	{
        for (int i = 0; i < mapWidth; i++)
        {
            for (int j = 0; j < mapHeight; j++)
            {
                graph[new Vector2(i, j)].obstacle = obstacles[i,j];
            }
        }
	}
}
