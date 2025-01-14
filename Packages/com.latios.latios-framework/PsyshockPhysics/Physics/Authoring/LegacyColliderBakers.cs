﻿using System.Collections.Generic;
using Latios.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Psyshock.Authoring.Systems
{
    [DisableAutoCreation]
    public class LegacySphereColliderBaker : Baker<UnityEngine.SphereCollider>
    {
        ColliderBakerHelper m_helper;

        public LegacySphereColliderBaker()
        {
            m_helper = new ColliderBakerHelper(this);
        }

        public override void Bake(UnityEngine.SphereCollider authoring)
        {
            if (!m_helper.ShouldBake(authoring))
                return;

            AddComponent((Collider) new SphereCollider
            {
                center      = authoring.center,
                radius      = authoring.radius,
                stretchMode = SphereCollider.StretchMode.StretchCenter
            });
        }
    }

    [DisableAutoCreation]
    public class LegacyCapsuleColliderBaker : Baker<UnityEngine.CapsuleCollider>
    {
        ColliderBakerHelper m_helper;

        public LegacyCapsuleColliderBaker()
        {
            m_helper = new ColliderBakerHelper(this);
        }

        public override void Bake(UnityEngine.CapsuleCollider authoring)
        {
            if (!m_helper.ShouldBake(authoring))
                return;

            float3 dir;
            if (authoring.direction == 0)
            {
                dir = new float3(1f, 0f, 0f);
            }
            else if (authoring.direction == 1)
            {
                dir = new float3(0f, 1, 0f);
            }
            else
            {
                dir = new float3(0f, 0f, 1f);
            }
            AddComponent((Collider) new CapsuleCollider
            {
                pointB      = (float3)authoring.center + ((authoring.height / 2f - authoring.radius) * dir),
                pointA      = (float3)authoring.center - ((authoring.height / 2f - authoring.radius) * dir),
                radius      = authoring.radius,
                stretchMode = CapsuleCollider.StretchMode.StretchPoints
            });
        }
    }

    [DisableAutoCreation]
    public class LegacyBoxColliderBaker : Baker<UnityEngine.BoxCollider>
    {
        ColliderBakerHelper m_helper;

        public LegacyBoxColliderBaker()
        {
            m_helper = new ColliderBakerHelper(this);
        }

        public override void Bake(UnityEngine.BoxCollider authoring)
        {
            if (!m_helper.ShouldBake(authoring))
                return;

            AddComponent((Collider) new BoxCollider
            {
                center   = authoring.center,
                halfSize = authoring.size / 2f
            });
        }
    }

    public struct LegacyConvexColliderBakerWorker : ISmartBakeItem<UnityEngine.MeshCollider>
    {
        SmartBlobberHandle<ConvexColliderBlob> m_handle;

        public bool Bake(UnityEngine.MeshCollider authoring, IBaker baker)
        {
            if (!authoring.convex)
                return false;

            if (!(baker as LegacyConvexColliderBaker).m_helper.ShouldBake(authoring))
                return false;

            m_handle = baker.RequestCreateBlobAsset(authoring.sharedMesh);
            return m_handle.IsValid;
        }

        public void PostProcessBlobRequests(EntityManager entityManager, Entity entity)
        {
            Collider collider = new ConvexCollider
            {
                convexColliderBlob = m_handle.Resolve(entityManager),
                scale              = 1f
            };
            entityManager.AddComponentData(entity, collider);
        }
    }

    [DisableAutoCreation]
    public class LegacyConvexColliderBaker : SmartBaker<UnityEngine.MeshCollider, LegacyConvexColliderBakerWorker>
    {
        internal ColliderBakerHelper m_helper;

        public LegacyConvexColliderBaker()
        {
            m_helper = new ColliderBakerHelper(this);
        }
    }

    public struct LegacyCompoundColliderBakerWorker : ISmartBakeItem<UnityEngine.Collider>
    {
        SmartBlobberHandle<CompoundColliderBlob> m_handle;
        float                                    m_scale;

        public bool Bake(UnityEngine.Collider authoring, IBaker baker)
        {
            var smartBaker = baker as LegacyCompoundColliderBaker;
            smartBaker.m_colliderCache.Clear();
            smartBaker.GetComponents(smartBaker.m_colliderCache);
            if (smartBaker.m_colliderCache.Count < 2)
                return false;

            smartBaker.m_compoundCache.Clear();
            smartBaker.GetComponentsInParent(smartBaker.m_compoundCache);
            foreach (var compoundAuthoring in smartBaker.m_compoundCache)
            {
                if (compoundAuthoring.colliderType == AuthoringColliderTypes.None)
                    continue;
                if (!compoundAuthoring.enabled)
                    continue;
                if (compoundAuthoring.generateFromChildren)
                    return false;

                foreach (var child in compoundAuthoring.colliders)
                {
                    if (child.gameObject == authoring.gameObject)
                        return false;
                }
            }

            // We only want to process once for all colliders. If any collider is modified, added, or removed,
            // dependencies will trigger a rebake for all colliders due to GetComponents(), so it only matters
            // that one is picked for a single bake but which gets picked doesn't matter.
            // But for consistency, we need to acquire other components to perform additional checks.
            var transform = smartBaker.GetComponent<UnityEngine.Transform>();

            if (smartBaker.m_colliderCache[0].GetInstanceID() != authoring.GetInstanceID())
                return false;

            for (int i = 0; i < smartBaker.m_colliderCache.Count; i++)
            {
                if (!smartBaker.m_colliderCache[i].enabled)
                {
                    smartBaker.m_colliderCache.RemoveAtSwapBack(i);
                    i--;
                }
            }
            if (smartBaker.m_colliderCache.Count < 2)
                return false;

            m_handle = smartBaker.RequestCreateBlobAsset(smartBaker.m_colliderCache, transform);
            return true;
        }

        public void PostProcessBlobRequests(EntityManager entityManager, Entity entity)
        {
            Collider collider = new CompoundCollider
            {
                compoundColliderBlob = m_handle.Resolve(entityManager),
                scale                = 1f,
                stretch              = 1f
            };
            entityManager.AddComponentData(entity, collider);
        }
    }

    [BakeDerivedTypes]
    [DisableAutoCreation]
    public class LegacyCompoundColliderBaker : SmartBaker<UnityEngine.Collider, LegacyCompoundColliderBakerWorker>
    {
        internal List<UnityEngine.Collider> m_colliderCache = new List<UnityEngine.Collider>();
        internal List<ColliderAuthoring>    m_compoundCache = new List<ColliderAuthoring>();
    }

    internal class ColliderBakerHelper
    {
        IBaker                     m_baker;
        List<UnityEngine.Collider> m_colliderCache = new List<UnityEngine.Collider>();
        List<ColliderAuthoring>    m_compoundCache = new List<ColliderAuthoring>();

        public ColliderBakerHelper(IBaker baker)
        {
            m_baker = baker;
        }

        public bool ShouldBake(UnityEngine.Collider authoring)
        {
            if (!authoring.enabled)
                return false;

            m_colliderCache.Clear();
            m_baker.GetComponents(m_colliderCache);
            if (m_colliderCache.Count > 1)
            {
                int enabledCount = 0;
                foreach (var c in m_colliderCache)
                    enabledCount += c.enabled ? 1 : 0;
                if (enabledCount > 1)
                    return false;
            }
            m_compoundCache.Clear();
            m_baker.GetComponentsInParent(m_compoundCache);
            foreach (var compoundAuthoring in m_compoundCache)
            {
                if (compoundAuthoring.colliderType == AuthoringColliderTypes.None)
                    continue;
                if (!compoundAuthoring.enabled)
                    continue;
                if (compoundAuthoring.generateFromChildren)
                    return false;

                foreach (var child in compoundAuthoring.colliders)
                {
                    if (child.gameObject == authoring.gameObject)
                        return false;
                }
            }

            return true;
        }
    }
}

