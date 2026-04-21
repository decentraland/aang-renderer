using UnityEngine;

namespace SpringBones
{
    public readonly struct SpringBoneData
    {
        public readonly Transform ManagedTransform;
        public readonly bool IsRoot;
        public readonly float Stiffness;
        public readonly float Drag;
        public readonly Vector3 GravityDir;
        public readonly float GravityPower;
        public readonly float HitRadius;
        public readonly Quaternion InitialLocalRotation;

        public SpringBoneData(Transform managedTransform,
            bool isRoot,
            float stiffness,
            float drag,
            Vector3 gravityDir,
            float gravityPower,
            float hitRadius,
            Quaternion initialLocalRotation)
        {
            ManagedTransform = managedTransform;
            IsRoot = isRoot;
            Stiffness = stiffness;
            Drag = drag;
            GravityDir = gravityDir;
            GravityPower = gravityPower;
            HitRadius = hitRadius;
            InitialLocalRotation = initialLocalRotation;
        }
    }
}
