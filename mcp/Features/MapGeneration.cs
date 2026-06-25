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
        int width = (int)size.X;
        int height = (int)size.Y;
        int depth = (int)size.Z;

        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if(y == height - 1) blocks.AddCube(new Vector3(x, y, z), 1);
                    else blocks.AddCube(new Vector3(x, y, z), 0);
                }
            }
        }
    }
}
