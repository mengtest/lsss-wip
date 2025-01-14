using Latios.Transforms;
using Latios.Transforms.Systems;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Entities.SystemAPI;

namespace Latios.Kinemation.Systems
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PreTransformSuperSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct CopyTransformFromBoneSystem : ISystem
    {
        EntityQuery m_query;

        public void OnCreate(ref SystemState state)
        {
            m_query = state.Fluent().WithAll<LocalTransform>(false).WithAll<CopyLocalToParentFromBone>(true).WithAll<BoneOwningSkeletonReference>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new CopyFromBoneJob
            {
                fromBoneHandle    = GetComponentTypeHandle<CopyLocalToParentFromBone>(true),
                skeletonHandle    = GetComponentTypeHandle<BoneOwningSkeletonReference>(true),
                obtLookup         = GetBufferLookup<OptimizedBoneTransform>(true),
                stateLookup       = GetComponentLookup<OptimizedSkeletonState>(true),
                ltHandle          = GetComponentTypeHandle<LocalTransform>(false),
                lastSystemVersion = state.LastSystemVersion
            }.ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        struct CopyFromBoneJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<CopyLocalToParentFromBone>   fromBoneHandle;
            [ReadOnly] public ComponentTypeHandle<BoneOwningSkeletonReference> skeletonHandle;
            [ReadOnly] public BufferLookup<OptimizedBoneTransform>             obtLookup;
            [ReadOnly] public ComponentLookup<OptimizedSkeletonState>          stateLookup;
            public ComponentTypeHandle<LocalTransform>                         ltHandle;

            public uint lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var skeletons = chunk.GetNativeArray(ref skeletonHandle);

                if (!chunk.DidChange(ref fromBoneHandle, lastSystemVersion) && !chunk.DidChange(ref skeletonHandle, lastSystemVersion))
                {
                    bool needsCopy = false;
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (obtLookup.DidChange(skeletons[i].skeletonRoot, lastSystemVersion))
                        {
                            needsCopy = true;
                            break;;
                        }
                    }

                    if (!needsCopy)
                        return;
                }

                var bones = chunk.GetNativeArray(ref fromBoneHandle);
                var lts   = chunk.GetNativeArray(ref ltHandle).Reinterpret<TransformQvvs>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    var buffer    = obtLookup[skeletons[i].skeletonRoot];
                    var state     = stateLookup[skeletons[i].skeletonRoot].state;
                    var boneCount = buffer.Length / 6;
                    var mask      = (byte)(state & OptimizedSkeletonState.Flags.RotationMask);
                    var root      = 0;
                    if ((state & OptimizedSkeletonState.Flags.IsDirty) == OptimizedSkeletonState.Flags.IsDirty)
                    {
                        root = OptimizedSkeletonState.CurrentFromMask[mask];
                    }
                    else
                    {
                        root = OptimizedSkeletonState.PreviousFromMask[mask];
                    }
                    lts[i] = buffer[bones[i].boneIndex + root * boneCount * 2].boneTransform;
                }
            }
        }
    }
}

