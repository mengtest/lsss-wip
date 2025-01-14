using System.Collections.Generic;
using System.Reflection;
using Latios.Transforms.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Latios.Kinemation.Authoring
{
    class MeshRendererBakingUtility
    {
        struct CopyParentRequestTag : IRequestCopyParentTransform { }

        struct LODState
        {
            public LODGroup LodGroup;
            public Entity   LodGroupEntity;
            public int      LodGroupIndex;
        }

        static void CreateLODState<T>(Baker<T> baker, Renderer authoringSource, out LODState lodState) where T : Component
        {
            // LODGroup
            lodState                = new LODState();
            lodState.LodGroup       = baker.GetComponentInParent<LODGroup>();
            lodState.LodGroupEntity = baker.GetEntity(lodState.LodGroup);
            lodState.LodGroupIndex  = FindInLODs(lodState.LodGroup, authoringSource);
        }

        private static int FindInLODs(LODGroup lodGroup, Renderer authoring)
        {
            if (lodGroup != null)
            {
                var lodGroupLODs = lodGroup.GetLODs();

                // Find the renderer inside the LODGroup
                for (int i = 0; i < lodGroupLODs.Length; ++i)
                {
                    foreach (var renderer in lodGroupLODs[i].renderers)
                    {
                        if (renderer == authoring)
                        {
                            return i;
                        }
                    }
                }
            }
            return -1;
        }

#pragma warning disable CS0162
        private static void AddRendererComponents<T>(Entity entity, Baker<T> baker, in RenderMeshDescription renderMeshDescription, RenderMesh renderMesh) where T : Component
        {
            // Entities with Static are never rendered with motion vectors
            bool inMotionPass = RenderMeshUtility.kUseHybridMotionPass &&
                                renderMeshDescription.FilterSettings.IsInMotionPass &&
                                !baker.IsStatic();

            RenderMeshUtility.EntitiesGraphicsComponentFlags flags = RenderMeshUtility.EntitiesGraphicsComponentFlags.Baking;
            if (inMotionPass)
                flags |= RenderMeshUtility.EntitiesGraphicsComponentFlags.InMotionPass;
            flags     |= RenderMeshUtility.LightProbeFlags(renderMeshDescription.LightProbeUsage);
            flags     |= RenderMeshUtility.DepthSortedFlags(renderMesh.material);

            // Add all components up front using as few calls as possible.
            var componentTypes = RenderMeshUtility.s_EntitiesGraphicsComponentTypes.GetComponentTypes(flags);
            baker.AddComponent(entity, componentTypes);

            baker.SetSharedComponentManaged(entity, renderMesh);
            baker.SetSharedComponentManaged(entity, renderMeshDescription.FilterSettings);

            var localBounds                                     = renderMesh.mesh.bounds.ToAABB();
            baker.SetComponent(entity, new RenderBounds { Value = localBounds });
        }

        internal static void Convert<T>(Baker<T>              baker,
                                        Renderer authoring,
                                        Mesh mesh,
                                        List<Material>        sharedMaterials,
                                        bool attachToPrimaryEntityForSingleMaterial,
                                        out List<Entity>      additionalEntities,
                                        UnityEngine.Transform root = null) where T : Component
        {
            additionalEntities = new List<Entity>();

            if (mesh == null || sharedMaterials.Count == 0)
            {
                Debug.LogWarning(
                    $"Renderer is not converted because either the assigned mesh is null or no materials are assigned on GameObject {authoring.name}.",
                    authoring);
                return;
            }

            // Takes a dependency on the material
            foreach (var material in sharedMaterials)
                baker.DependsOn(material);

            // Takes a dependency on the mesh
            baker.DependsOn(mesh);

            // RenderMeshDescription accesses the GameObject layer.
            // Declaring the dependency on the GameObject with GetLayer, so the baker rebakes if the layer changes
            baker.GetLayer(authoring);
            var desc       = new RenderMeshDescription(authoring);
            var renderMesh = new RenderMesh(authoring, mesh, sharedMaterials);

            // Always disable per-object motion vectors for static objects
            if (baker.IsStatic())
            {
                if (desc.FilterSettings.MotionMode == MotionVectorGenerationMode.Object)
                    desc.FilterSettings.MotionMode = MotionVectorGenerationMode.Camera;
            }

            if (attachToPrimaryEntityForSingleMaterial && sharedMaterials.Count == 1)
            {
                ConvertToSingleEntity(
                    baker,
                    desc,
                    renderMesh,
                    authoring);
            }
            else
            {
                ConvertToMultipleEntities(
                    baker,
                    desc,
                    renderMesh,
                    authoring,
                    sharedMaterials,
                    root,
                    out additionalEntities);
            }
        }

#pragma warning restore CS0162

        static void ConvertToSingleEntity<T>(
            Baker<T>              baker,
            RenderMeshDescription renderMeshDescription,
            RenderMesh renderMesh,
            Renderer renderer) where T : Component
        {
            CreateLODState(baker, renderer, out var lodState);

            var entity = baker.GetEntity(renderer);

            AddRendererComponents(entity, baker, renderMeshDescription, renderMesh);

            if (lodState.LodGroupEntity != Entity.Null && lodState.LodGroupIndex != -1)
            {
                var lodComponent = new MeshLODComponent { Group = lodState.LodGroupEntity, LODMask = 1 << lodState.LodGroupIndex };
                baker.AddComponent(entity, lodComponent);
            }
        }

        internal static void ConvertToMultipleEntities<T>(
            Baker<T>              baker,
            RenderMeshDescription renderMeshDescription,
            RenderMesh renderMesh,
            Renderer renderer,
            List<Material>        sharedMaterials,
            UnityEngine.Transform root,
            out List<Entity>      additionalEntities) where T : Component
        {
            CreateLODState(baker, renderer, out var lodState);

            int materialCount  = sharedMaterials.Count;
            additionalEntities = new List<Entity>();

            var rootQvvs = Latios.Transforms.TransformQvvs.identity;
            if (root != null)
                rootQvvs = root.GetWorldSpaceQvvs();

            for (var m = 0; m != materialCount; m++)
            {
                Entity meshEntity;
                if (root == null)
                {
                    meshEntity = baker.CreateAdditionalEntity(TransformUsageFlags.Default, false, $"{baker.GetName()}-MeshRendererEntity");

                    // Update Transform components:
                    baker.AddComponent<AdditionalMeshRendererEntity>(meshEntity);
                    if (!baker.IsStatic())
                        baker.AddComponent<CopyParentRequestTag>(meshEntity);
                }
                else
                {
                    meshEntity = baker.CreateAdditionalEntity(TransformUsageFlags.ManualOverride, false, $"{baker.GetName()}-MeshRendererEntity");

                    baker.AddComponent(meshEntity, new Latios.Transforms.WorldTransform { worldTransform = rootQvvs });

                    if (!baker.IsStatic())
                    {
                        var rootEntity = baker.GetEntity(
                            root);
                        baker.AddComponent(                      meshEntity, new Latios.Transforms.Parent { parent = rootEntity });
                        baker.AddComponent<CopyParentRequestTag>(meshEntity);
                    }
                }

                additionalEntities.Add(meshEntity);

                var material = sharedMaterials[m];

                renderMesh.subMesh  = m;
                renderMesh.material = material;

                AddRendererComponents(
                    meshEntity,
                    baker,
                    renderMeshDescription,
                    renderMesh);

                if (lodState.LodGroupEntity != Entity.Null && lodState.LodGroupIndex != -1)
                {
                    var lodComponent = new MeshLODComponent { Group = lodState.LodGroupEntity, LODMask = 1 << lodState.LodGroupIndex };
                    baker.AddComponent(meshEntity, lodComponent);
                }
            }
        }
    }
}

