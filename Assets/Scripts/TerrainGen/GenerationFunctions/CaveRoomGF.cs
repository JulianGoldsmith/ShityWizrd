using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

[CreateAssetMenu(fileName = "CaveRooms", menuName = "TerrainGen/GenerationFunctions/Cave Rooms")]
public class CaveRoomGF : GenerationFunctions
{
    public List<SDFPrimitive> roomShapes = new List<SDFPrimitive>();
    public List<NoiseLayer> roomShapesNoise = new List<NoiseLayer>();

    [Header("Floor Height %"), Range (0,1)]
    public float floorHeightPercent = 0;
    public NoiseLayer floorNoise; 
    public float floorBlendFactor = 4f;
    public VoxelMat floorMaterial = VoxelMat.Dirt;

    //initiation called once prior to the looping through each voxel process. 
    public override GenerationFunctions InitAndClone(GeneratedRoom roomInstance)
    {
        //give shape an offset(relative to the room center) that leaves it within the bounds of the room using its size,
        CaveRoomGF fn = (CaveRoomGF)base.InitAndClone(roomInstance);

        var newShapes = new List<SDFPrimitive>(fn.roomShapes.Count);

        float roomRadius = roomInstance.radius;
        foreach (SDFPrimitive shape in fn.roomShapes)
        {
            shape.offset.x = Random.Range(-(roomRadius - shape.size.x ), roomRadius - shape.size.x);
            shape.offset.y = Random.Range(-(roomRadius - shape.size.y ), roomRadius - shape.size.y );
            shape.offset.z = Random.Range(-(roomRadius - shape.size.z ), roomRadius - shape.size.z );
        }
        return fn;
    }

    public override VoxelData GenerateVoxelData(VoxelData voxelData, Vector3 pos, GeneratedRoom roomInstance)
    {
        float cap = roomInstance.sdfCap;
        float smoothing = 0f;

        float density = voxelData.density;

        float roomRadius = roomInstance.radius;

        float caveSolid = -cap; // start at eg -10

        foreach (SDFPrimitive shape in roomShapes)
        {
            //rooms
            Vector3 center = roomInstance.pos + shape.offset;

            float roomShape = shape.Generate(pos, roomInstance.pos); //generate a positive inside shape 
            
            

            caveSolid = SDFHelpers.Union(caveSolid, roomShape, cap, softTruncate: true, k: smoothing); //union the positive shapes with smoothing and truncation

        }

        foreach (var n in roomShapesNoise)
        {
            if (n.enabled)
            {
                float influenceMultiplier = CalculateInfluenceMultiplier(n.influences, voxelData);
                float noiseValue = Noise.Generate(pos.x * n.frequency, pos.y * n.frequency, pos.z * n.frequency) * n.amplitude * influenceMultiplier;
                caveSolid -= noiseValue;
            }

        }

        //subtract the positive caveSolid from the density. 

        density = SDFHelpers.Subtract(density, caveSolid, cap, softTruncate: true, k: smoothing);

        if (density > 0f)
        {
            float hostRockM = Mathf.Min(density, cap/5);
            //MaterialFields.Propose(ref voxelData, (byte)VoxelMat.Stone, hostRockM);
        }

        #region flooor
        bool generateFloor = true;
        if (generateFloor)
        {
            float baseFloorHeight = (roomInstance.pos.y - (roomInstance.radius)) + (roomInstance.radius * floorHeightPercent * 2);
            float floorHeight = baseFloorHeight;


            if (floorNoise.enabled)
            {
                float influenceMultiplier = CalculateInfluenceMultiplier(floorNoise.influences, voxelData);
                float noise = Noise.Generate(pos.x * floorNoise.frequency, pos.y * floorNoise.frequency, pos.z * floorNoise.frequency) * floorNoise.amplitude * influenceMultiplier;
                floorHeight -= noise;
            }

            float floorHalfspace = floorHeight - pos.y;

            float floorSolid = SDFHelpers.Intersect(caveSolid, floorHalfspace, cap, softTruncate: false, k: floorBlendFactor);

            float before = density;

            float after = SDFHelpers.Union(before, floorSolid, cap, softTruncate: true, k: floorBlendFactor);
            density = after;

            bool becameSolidHere = (before <= 0f && after > 0f);
            
            if (floorSolid > 0f)
            {
                voxelData.matId0 = (byte)floorMaterial;
                //MaterialFields.HardSetMaterial(voxelData, (int)floorMaterial);
                //Debug.Log("floor solid met");
            }

        }

        #endregion




        /*
        if (pos.z < 30)
        { 
            MaterialStamp.Stamp(ref voxelData, (byte)floorMaterial, priority: 250, requireNearSurface: false, surfaceBand: 2f);
           // Debug.Log("floor solid met");
        }
        */
        voxelData.density = density;

        return voxelData;
    }


}

