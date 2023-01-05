﻿using System;
using System.Diagnostics;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class Physics
    {
        /// <summary>
        /// Bakes the scale and stretch values into the collider's shape.
        /// For some collider types, the resulting collider may only be an approximation.
        /// Such colliders usually have a StretchMode enum to control the behavior.
        /// </summary>
        /// <param name="collider">The collider to bake the scale and stretch into</param>
        /// <param name="scale">A uniform scale of the collider</param>
        /// <param name="stretch">A non-uniform squash/stretch of the collider in the collider's local space</param>
        public static void ScaleStretchCollider(ref Collider collider, float scale, float3 stretch)
        {
            if (math.all(new float4(stretch, scale) == 1f))
                return;

            switch (collider.type)
            {
                case ColliderType.Sphere:
                    ScaleStretchCollider(ref collider.m_sphere,   scale, stretch);
                    break;
                case ColliderType.Capsule:
                    ScaleStretchCollider(ref collider.m_capsule,  scale, stretch);
                    break;
                case ColliderType.Box:
                    ScaleStretchCollider(ref collider.m_box,      scale, stretch);
                    break;
                case ColliderType.Triangle:
                    ScaleStretchCollider(ref collider.m_triangle, scale, stretch);
                    break;
                case ColliderType.Convex:
                    ScaleStretchCollider(ref collider.m_convex,   scale, stretch);
                    break;
                case ColliderType.Compound:
                    ScaleStretchCollider(ref collider.m_compound, scale, stretch);
                    break;
                default:
                    ThrowUnsupportedType();
                    break;
            }
        }

        /// <summary>
        /// Bakes the scale and stretch values into the collider's shape as a new collider.
        /// For some collider types, the resulting collider may only be an approximation.
        /// Such colliders usually have a StretchMode enum to control the behavior.
        /// </summary>
        /// <param name="collider">The collider to use as a base when baking the scale and stretch</param>
        /// <param name="scale">A uniform scale of the collider</param>
        /// <param name="stretch">A non-uniform squash/stretch of the collider in the collider's local space</param>
        /// <returns>A new collider with the baked scale and stretch</returns>
        public static Collider ScaleStretchCollider(Collider collider, float scale, float3 stretch)
        {
            ScaleStretchCollider(ref collider, scale, stretch);
            return collider;
        }

        internal static void ScaleStretchCollider(ref SphereCollider sphere, float scale, float3 stretch)
        {
            switch (sphere.stretchMode)
            {
                case SphereCollider.StretchMode.StretchCenter:
                    sphere.center *= stretch * scale;
                    sphere.radius *= scale;
                    break;
                case SphereCollider.StretchMode.IgnoreStretch:
                    sphere.radius *= scale;
                    break;
            }
        }

        internal static SphereCollider ScaleStretchCollider(SphereCollider sphere, float scale, float3 stretch)
        {
            ScaleStretchCollider(ref sphere, scale, stretch);
            return sphere;
        }

        internal static void ScaleStretchCollider(ref CapsuleCollider capsule, float scale, float3 stretch)
        {
            switch(capsule.stretchMode)
            {
                case CapsuleCollider.StretchMode.StretchPoints:
                    capsule.pointA *= stretch * scale;
                    capsule.pointB *= stretch * scale;
                    capsule.radius *= scale;
                    break;
                case CapsuleCollider.StretchMode.IgnoreStretch:
                {
                    capsule.pointA *= scale;
                    capsule.pointB *= scale;
                    capsule.radius *= scale;
                    break;
                }
            }
        }

        internal static CapsuleCollider ScaleStretchCollider(CapsuleCollider capsule, float scale, float3 stretch)
        {
            ScaleStretchCollider(ref capsule, scale, stretch);
            return capsule;
        }

        internal static void ScaleStretchCollider(ref BoxCollider box, float scale, float3 stretch)
        {
            var newPositive = (box.center + box.halfSize) * stretch * scale;
            var newNegative = (box.center - box.halfSize) * stretch * scale;
            box.center      = (newPositive + newNegative) / 2f;
            box.halfSize    = math.abs(newPositive - box.center);
        }

        internal static BoxCollider ScaleStretchCollider(BoxCollider box, float scale, float3 stretch)
        {
            ScaleStretchCollider(ref box, scale, stretch);
            return box;
        }

        internal static void ScaleStretchCollider(ref TriangleCollider triangle, float scale, float3 stretch)
        {
            var factor       = scale * stretch;
            triangle.pointA *= factor;
            triangle.pointB *= factor;
            triangle.pointC *= factor;
        }

        internal static TriangleCollider ScaleStretchCollider(TriangleCollider triangle, float scale, float3 stretch)
        {
            ScaleStretchCollider(ref triangle, scale, stretch);
            return triangle;
        }

        internal static void ScaleStretchCollider(ref ConvexCollider convex, float scale, float3 stretch)
        {
            convex.scale *= stretch * scale;
        }

        internal static ConvexCollider ScaleStretchCollider(ConvexCollider convex, float scale, float3 stretch)
        {
            ScaleStretchCollider(ref convex, scale, stretch);
            return convex;
        }

        internal static void ScaleStretchCollider(ref CompoundCollider compound, float scale, float3 stretch)
        {
            switch (compound.stretchMode)
            {
                case CompoundCollider.StretchMode.RotateStretchLocally:
                    compound.scale   *= scale;
                    compound.stretch *= stretch;
                    break;
                case CompoundCollider.StretchMode.IgnoreStretch:
                    compound.scale *= scale;
                    break;
                case CompoundCollider.StretchMode.StretchPositionsOnly:
                    compound.scale   *= scale;
                    compound.stretch *= stretch;
                    break;
            }
        }

        internal static CompoundCollider ScaleStretchCollider(CompoundCollider compound, float scale, float3 stretch)
        {
            ScaleStretchCollider(ref compound, scale, stretch);
            return compound;
        }

        // Todo: Legacy, remove
        public static Collider ScaleCollider(in Collider collider, PhysicsScale scale)
        {
            switch (collider.type)
            {
                case ColliderType.Sphere: return ScaleCollider((SphereCollider)collider, scale);
                case ColliderType.Capsule: return ScaleCollider((CapsuleCollider)collider, scale);
                case ColliderType.Box: return ScaleCollider((BoxCollider)collider, scale);
                case ColliderType.Triangle: return ScaleCollider((TriangleCollider)collider, scale);
                case ColliderType.Convex: return ScaleCollider((ConvexCollider)collider, scale);
                case ColliderType.Compound: return ScaleCollider((CompoundCollider)collider, scale);
                default: ThrowUnsupportedType(); return new Collider();
            }
        }

        public static SphereCollider ScaleCollider(SphereCollider sphere, PhysicsScale scale)
        {
            CheckNoOrUniformScale(scale, ColliderType.Sphere);
            sphere.center *= scale.scale.x;
            sphere.radius *= scale.scale.x;
            return sphere;
        }

        public static CapsuleCollider ScaleCollider(CapsuleCollider capsule, PhysicsScale scale)
        {
            CheckNoOrUniformScale(scale, ColliderType.Capsule);
            capsule.pointA *= scale.scale.x;
            capsule.pointB *= scale.scale.x;
            capsule.radius *= scale.scale.x;
            return capsule;
        }

        public static BoxCollider ScaleCollider(BoxCollider box, PhysicsScale scale)
        {
            CheckNoOrValidScale(scale, ColliderType.Box);
            box.center   *= scale.scale;
            box.halfSize *= scale.scale;
            return box;
        }

        public static TriangleCollider ScaleCollider(TriangleCollider triangle, PhysicsScale scale)
        {
            CheckNoOrValidScale(scale, ColliderType.Triangle);
            triangle.pointA *= scale.scale;
            triangle.pointB *= scale.scale;
            triangle.pointC *= scale.scale;
            return triangle;
        }

        public static ConvexCollider ScaleCollider(ConvexCollider convex, PhysicsScale scale)
        {
            CheckNoOrValidScale(scale, ColliderType.Convex);
            convex.scale *= scale.scale;
            return convex;
        }

        public static CompoundCollider ScaleCollider(CompoundCollider compound, PhysicsScale scale)
        {
            CheckNoOrUniformScale(scale, ColliderType.Compound);
            compound.scale *= scale.scale.x;
            return compound;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckNoOrUniformScale(PhysicsScale scale, ColliderType type)
        {
            if (scale.state == PhysicsScale.State.NonComputable | scale.state == PhysicsScale.State.NonUniform)
            {
                switch (type)
                {
                    case ColliderType.Sphere: throw new InvalidOperationException("Sphere Collider must be scaled with no scale or uniform scale.");
                    case ColliderType.Capsule: throw new InvalidOperationException("Capsule Collider must be scaled with no scale or uniform scale.");
                    case ColliderType.Box: throw new InvalidOperationException("Box Collider must be scaled with no scale or uniform scale.");
                    case ColliderType.Compound: throw new InvalidOperationException("Compound Collider must be scaled with no scale or uniform scale.");
                    default: throw new InvalidOperationException("Failed to scale unknown collider type.");
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckNoOrValidScale(PhysicsScale scale, ColliderType type)
        {
            if (scale.state == PhysicsScale.State.NonComputable)
            {
                throw new InvalidOperationException("The collider cannot be scaled with a noncomputable scale");
            }
        }
    }
}

