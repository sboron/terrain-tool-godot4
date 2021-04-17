using System.Data.SqlTypes;
using System.Transactions;
using System.Runtime.InteropServices;
using Godot;
using System;

namespace TerrainEditor
{
    [Tool]
    public partial class TerrainChunk : Resource
    {
        [Export]
        public Vector2i position = new Vector2i();

        [Export]
        public Vector2 offsetUv = new Vector2();

        public AABB bounds = new AABB();

        [Export]
        public float TerrainChunkSizeLOD0 = 0.0f;
        public Mesh mesh { get; set; }

        [Export]
        public float offset = 0;

        [Export]
        public float height = 1;

        public float ChunkSizeNextLOD = 0.0f;
        public float lod = 0.0f;

        protected RID instanceRid;

        protected RID materialId;

        protected Material materialInUse;

        protected RID meshId;

        protected Godot.Collections.Array<TerrainChunk> _neighbors = new Godot.Collections.Array<TerrainChunk>();

        [Export]
        public int _cachedDrawLOD;

        public AABB getBounds(TerrainPatchInfo info, Vector3 patchOffset)
        {
            float size = (float)info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            var origin = patchOffset + new Vector3(position.x * size, offset, position.y * size);
            bounds = new AABB(origin, new Vector3(size, height, size));

            return bounds;
        }

        protected Plane getNeightbors()
        {
            int lod = _cachedDrawLOD;
            int minLod = Math.Max(lod + 1, 0);

            Plane pl = new Plane();
            pl.x = (float)Math.Clamp(_neighbors[0]._cachedDrawLOD, lod, minLod);
            pl.y = (float)Math.Clamp(_neighbors[1]._cachedDrawLOD, lod, minLod);
            pl.z = (float)Math.Clamp(_neighbors[2]._cachedDrawLOD, lod, minLod);
            pl.D = (float)Math.Clamp(_neighbors[3]._cachedDrawLOD, lod, minLod);

            return pl;
        }

        public void ClearDraw()
        {

            if (meshId != null)
            {
                RenderingServer.FreeRid(meshId);
            }

            if (instanceRid != null)
            {
                RenderingServer.FreeRid(instanceRid);
            }

            mesh = null;
            materialInUse = null;
            meshId = null;
            materialId = null;
            instanceRid = null;
        }

        public Transform UpdateTransform(TerrainPatchInfo info, Transform terrainTransform, Vector3 patchoffset)
        {
            float size = (info.chunkSize) * Terrain3D.TERRAIN_UNITS_PER_VERTEX;
            var localPosition = patchoffset + new Vector3(position.x * size, info.patchOffset, position.y * size);
            Transform localTransform = new Transform();
            localTransform.origin = localPosition;
            localTransform.basis = new Basis(Quat.Identity);
            localTransform.basis = localTransform.basis.Scaled(new Vector3(1.0f, info.patchHeight, 1.0f));

            var global = terrainTransform * localTransform;

            if (instanceRid != null)
                RenderingServer.InstanceSetTransform(instanceRid, global);
            if (materialId != null)
                RenderingServer.MaterialSetParam(materialId, "uv_scale", getUVScale());

            return global;
        }

        public void UpdateHeightmap(TerrainPatchInfo info)
        {

            //  if (instanceRid != null)
            //   RenderingServer.InstanceSetCustomAabb(instanceRid, new AABB(new Vector3(), new Vector3(size, offset, size)));
            if (materialId != null)
                RenderingServer.MaterialSetParam(materialId, "uv_scale", getUVScale());
        }

        private Plane getUVScale()
        {
            var q = new Quat(1.0f, 1.0f, position.x, position.y) * (1.0f / Terrain3D.CHUNKS_COUNT_EDGE);
            return new Plane(q.x, q.y, q.z, q.w);
        }

        public void UpdateShaderTexture(ref ImageTexture heightMap)
        {
            RenderingServer.MaterialSetParam(materialId, "heigtmap", heightMap);
        }

        public void UpdateInspectorMaterial(Color color, Plane BrushData0, Plane BrushData1)
        {
            RenderingServer.MaterialSetParam(materialId, "Color", color);
            RenderingServer.MaterialSetParam(materialId, "BrushData0", BrushData0);
            RenderingServer.MaterialSetParam(materialId, "BrushData1", BrushData1);
        }

        public void SetDefaultMaterial(Texture2D image)
        {
            RenderingServer.MaterialSetParam(materialId, "terrainDefaultMaterial", image);
        }


        public void CacheNeighbors(Terrain3D terrainNode, TerrainPatch currentPatch)
        {
            _neighbors.Clear();
            _neighbors.Add(this);
            _neighbors.Add(this);
            _neighbors.Add(this);
            _neighbors.Add(this);

            // 0: bottom
            if (position.y > 0)
            {
                _neighbors[0] = currentPatch.chunks[(position.y - 1) * Terrain3D.CHUNKS_COUNT_EDGE + position.x];
            }
            else
            {
                var patch = terrainNode.GetPatch(position.x, position.y - 1);
                if (patch != null)
                    _neighbors[0] = patch.chunks[(Terrain3D.CHUNKS_COUNT_EDGE - 1) * Terrain3D.CHUNKS_COUNT_EDGE + position.x];
            }

            // 1: left
            if (position.x > 0)
            {
                _neighbors[1] = currentPatch.chunks[position.y * Terrain3D.CHUNKS_COUNT_EDGE + (position.x - 1)];
            }
            else
            {
                var patch = terrainNode.GetPatch(position.x - 1, position.y);
                if (patch != null)
                    _neighbors[1] = patch.chunks[position.y * Terrain3D.CHUNKS_COUNT_EDGE + (Terrain3D.CHUNKS_COUNT_EDGE - 1)];
            }

            // 2: right 
            if (position.x < Terrain3D.CHUNKS_COUNT_EDGE - 1)
            {
                _neighbors[2] = currentPatch.chunks[position.y * Terrain3D.CHUNKS_COUNT_EDGE + (position.x + 1)];
            }
            else
            {
                var patch = terrainNode.GetPatch(position.x + 1, position.y);
                if (patch != null)
                    _neighbors[2] = patch.chunks[position.y * Terrain3D.CHUNKS_COUNT_EDGE];
            }

            // 3: top
            if (position.y < Terrain3D.CHUNKS_COUNT_EDGE - 1)
            {
                _neighbors[3] = currentPatch.chunks[(position.y + 1) * Terrain3D.CHUNKS_COUNT_EDGE + position.x];
            }
            else
            {
                var patch = terrainNode.GetPatch(position.x, position.y + 1);
                if (patch != null)
                    _neighbors[3] = patch.chunks[position.x];
            }
        }



        public void Draw(TerrainPatch patch, TerrainPatchInfo info, RID scenario, ref ImageTexture heightMap, ref Godot.Collections.Array<ImageTexture> splatMaps, Terrain3D tf, Vector3 patchoffset, Material mat)
        {
            _cachedDrawLOD = 0;
            int lod = _cachedDrawLOD;
            int minLod = Math.Max(lod + 1, 0);
            int chunkSize = info.chunkSize;

            // var shaderRid = shader.GetRid();
            mesh = GenerateMesh(patch, info.chunkSize, 0);
            GD.Print("Generate mesh with: " + info.chunkSize);
            meshId = mesh.GetRid();

            float size = (float)info.chunkSize * Terrain3D.TERRAIN_UNITS_PER_VERTEX;

            instanceRid = RenderingServer.InstanceCreate();
            RenderingServer.InstanceSetScenario(instanceRid, scenario); //adding to the scene
            RenderingServer.InstanceSetBase(instanceRid, meshId);
            RenderingServer.InstanceAttachObjectInstanceId(instanceRid, tf.GetInstanceId()); // attach to node



            RenderingServer.MeshSetCustomAabb(meshId, new AABB(new Vector3(), new Vector3(size, height, size)));
            RenderingServer.InstanceSetCustomAabb(instanceRid, new AABB(new Vector3(), new Vector3(size, height, size)));

            materialInUse = mat.Duplicate() as Material;
            materialId = materialInUse.GetRid();

            RenderingServer.InstanceGeometrySetMaterialOverride(instanceRid, materialId);
            var nextChunkSizeLod = (float)(((info.chunkSize + 1) >> (lod + 1)) - 1);

            RenderingServer.MaterialSetParam(materialId, "terrainHeightMap", heightMap);
            RenderingServer.MaterialSetParam(materialId, "terrainChunkSize", TerrainChunkSizeLOD0);
            RenderingServer.MaterialSetParam(materialId, "terrainNextLodChunkSize", nextChunkSizeLod);

            RenderingServer.MaterialSetParam(materialId, "terrainUvScale", getUVScale());
            RenderingServer.MaterialSetParam(materialId, "terrainCurrentLodLevel", lod);
            RenderingServer.MaterialSetParam(materialId, "terrainSplatmap1", splatMaps[0]);
            RenderingServer.MaterialSetParam(materialId, "terrainSplatmap2", splatMaps[1]);
            RenderingServer.MaterialSetParam(materialId, "terrainNeighborLod", getNeightbors());
            RenderingServer.MaterialSetParam(materialId, "terrainSmoothing", true);

            offsetUv = new Vector2((float)(patch.patchCoord.x * Terrain3D.CHUNKS_COUNT_EDGE + position.x), (float)(patch.patchCoord.y * Terrain3D.CHUNKS_COUNT_EDGE + position.y));
            RenderingServer.MaterialSetParam(materialId, "terrainUvOffset", offsetUv);
            RenderingServer.InstanceSetVisible(instanceRid, false);

            UpdateSettings(tf);
            tf.ForceUpdateTransform();
        }

        public void UpdateSettings(Terrain3D tf)
        {
            if (!tf.IsInsideTree())
                return;

            RenderingServer.InstanceGeometrySetCastShadowsSetting(instanceRid, tf.castShadow);
            switch (tf.giMode)
            {
                case Terrain3D.GIMode.Disabled:
                    {
                        RenderingServer.InstanceGeometrySetFlag(instanceRid, RenderingServer.InstanceFlags.UseBakedLight, false);
                        RenderingServer.InstanceGeometrySetFlag(instanceRid, RenderingServer.InstanceFlags.UseDynamicGi, false);
                    }
                    break;
                case Terrain3D.GIMode.Baked:
                    {

                        RenderingServer.InstanceGeometrySetFlag(instanceRid, RenderingServer.InstanceFlags.UseBakedLight, true);
                        RenderingServer.InstanceGeometrySetFlag(instanceRid, RenderingServer.InstanceFlags.UseDynamicGi, false);
                    }
                    break;
                case Terrain3D.GIMode.Dynamic:
                    {
                        RenderingServer.InstanceGeometrySetFlag(instanceRid, RenderingServer.InstanceFlags.UseBakedLight, false);
                        RenderingServer.InstanceGeometrySetFlag(instanceRid, RenderingServer.InstanceFlags.UseDynamicGi, true);
                    }
                    break;
            }



            RenderingServer.InstanceSetExtraVisibilityMargin(instanceRid, tf.extraCullMargin);
            RenderingServer.InstanceSetVisible(instanceRid, tf.IsVisibleInTree());
        }

        public ArrayMesh GenerateMesh(TerrainPatch patch, int chunkSize, int lodIndex)
        {
            if (patch.meshCache.ContainsKey(lodIndex))
            {
                return patch.meshCache[lodIndex].Duplicate() as ArrayMesh;
            }

            int chunkSizeLOD0 = chunkSize;

            // Prepare
            int vertexCount = (chunkSize + 1) >> lodIndex; // 32
            chunkSize = vertexCount - 1;
            int indexCount = chunkSize * chunkSize * 2 * 3;
            int vertexCount2 = vertexCount * vertexCount;

            // Create vertex buffer
            float vertexTexelSnapTexCoord = 1.0f / chunkSize;


            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            for (int z = 0; z < vertexCount; z++)
            {
                for (int x = 0; x < vertexCount; x++)
                {
                    var buff = new Vector3(x * vertexTexelSnapTexCoord, 0f, z * vertexTexelSnapTexCoord);

                    // Smooth LODs morphing based on Barycentric coordinates to morph to the lower LOD near chunk edges
                    var coord = new Quat(buff.z, buff.x, 1.0f - buff.x, 1.0f - buff.z);

                    // Apply some contrast
                    const float AdjustPower = 0.3f;

                    var color = new Color();
                    color.r = Convert.ToSingle(Math.Pow(coord.x, AdjustPower));
                    color.g = Convert.ToSingle(Math.Pow(coord.y, AdjustPower));
                    color.b = Convert.ToSingle(Math.Pow(coord.z, AdjustPower));
                    color.a = Convert.ToSingle(Math.Pow(coord.w, AdjustPower));


                    st.SetColor(color);
                    st.SetUv(new Vector2(x * vertexTexelSnapTexCoord, z * vertexTexelSnapTexCoord));
                    

                    st.SetNormal(Vector3.Up);
                    st.AddVertex(buff); //x
                }
            }

            for (int z = 0; z < chunkSize; z++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    int i00 = (x + 0) + (z + 0) * vertexCount;
                    int i10 = (x + 1) + (z + 0) * vertexCount;
                    int i11 = (x + 1) + (z + 1) * vertexCount;
                    int i01 = (x + 0) + (z + 1) * vertexCount;

                    st.AddIndex(i00);
                    st.AddIndex(i10);
                    st.AddIndex(i11);

                    st.AddIndex(i00);
                    st.AddIndex(i11);
                    st.AddIndex(i01);
                }
            }

            st.GenerateTangents();

            var mesh = st.Commit();
            patch.meshCache.Add(lodIndex, mesh);

            return mesh.Duplicate() as ArrayMesh;
        }
    }
}
