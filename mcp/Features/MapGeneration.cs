using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace MCP.Features;

public class MapGeneration
{
    /// <summary>
    /// Size of the map.
    /// </summary>
    public Vector3 size { get; set; }

    public void GenMap(BlocksManagement blocks)
    {
        /*blocks.cubes.Clear();
        blocks.faceEmission?.Clear();
        blocks.faceVisible?.Clear();

        int width = (int)size.X;
        int height = (int)size.Y;
        int depth = (int)size.Z;

        int totalBlocks = width * height * depth;
        blocks.cubes.Capacity = totalBlocks;
        blocks.faceEmission = new List<float[]>(totalBlocks);
        blocks.faceVisible = new List<bool[]>(totalBlocks);

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 pos = new Vector3(x, y, z);
                    blocks.AddCubeOptimized(pos, 0);
                }
            }
        }*/

        int width = (int)size.X;
        int height = (int)size.Y;
        int depth = (int)size.Z;

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    blocks.AddCube(new Vector3(x, y, z), 0);
                }
            }
        }
    }
}
