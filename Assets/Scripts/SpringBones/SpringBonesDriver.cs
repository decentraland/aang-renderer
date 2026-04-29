using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace SpringBones
{
    public class SpringBonesDriver : MonoBehaviour
    {
        SpringBoneService service;
        readonly List<int> slotIndices = new();
        readonly List<Transform> chainRootParents = new();
        readonly List<GameObject> chainOwners = new();
        readonly List<string> chainRootBoneNames = new();
        readonly List<Transform[]> chainBones = new();
        readonly List<Quaternion[]> chainInitialRotations = new();

        readonly List<Transform> chainJoints = new();
        readonly List<SpringBoneJointConfig> chainConfigs = new();

        void Awake() => service = new SpringBoneService();

        void OnDestroy()
        {
            service?.Dispose();
            service = null;
        }

        public void UnregisterAll()
        {
            if (service == null) return;
            foreach (int s in slotIndices) service.UnregisterSpring(s);
            slotIndices.Clear();
            chainRootParents.Clear();
            chainOwners.Clear();
            chainRootBoneNames.Clear();
            chainBones.Clear();
            chainInitialRotations.Clear();
        }

        /// <summary>
        /// Replace the full set of spring chains for a wearable with the ones declared in
        /// <paramref name="paramsByBone"/>. Empty / null map clears all chains owned by
        /// <paramref name="owner"/>, restoring the bones to their captured rest pose (normal bones).
        /// Bones present in the map that were not spring bones before become spring bones.
        /// </summary>
        public void SetSpringChainsForWearable(GameObject owner, Dictionary<string, SpringBoneParamsDTO> paramsByBone)
        {
            if (service == null || owner == null) return;

            RemoveChainsForOwner(owner);

            if (paramsByBone == null || paramsByBone.Count == 0) return;

            var skinned = owner.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned == null)
            {
                Debug.LogError($"[SpringBones] '{owner.name}' has no SkinnedMeshRenderer; cannot build chains");
                return;
            }

            var boneSet = new HashSet<Transform>(skinned.bones);

            int rootCount = 0;
            foreach (var kv in paramsByBone)
                if (kv.Value.isRoot) rootCount++;
            // Fallback: if no entry is flagged as root (e.g. JSBridge payload, legacy data),
            // treat every entry as a root so chains still register.
            var treatAllAsRoots = rootCount == 0;

            foreach (var bone in skinned.bones)
            {
                if (bone == null) continue;
                if (!paramsByBone.TryGetValue(bone.name, out var paramsDto)) continue;
                if (!treatAllAsRoots && !paramsDto.isRoot) continue;

                chainJoints.Clear();
                chainConfigs.Clear();
                var rootConfig = BuildConfigFromDTO(paramsDto, bone.localRotation);
                chainJoints.Add(bone);
                chainConfigs.Add(rootConfig);
                CollectChainDescendants(bone, boneSet, paramsByBone, rootConfig);
                FlushChain(owner, bone.parent, bone.name);
            }
        }

        int RemoveChainsForOwner(GameObject owner)
        {
            int removed = 0;
            for (int i = slotIndices.Count - 1; i >= 0; i--)
            {
                if (chainOwners[i] != owner) continue;

                var bones = chainBones[i];
                var rots = chainInitialRotations[i];
                for (int j = 0; j < bones.Length; j++)
                    if (bones[j] != null) bones[j].localRotation = rots[j];

                service.UnregisterSpring(slotIndices[i]);
                slotIndices.RemoveAt(i);
                chainRootParents.RemoveAt(i);
                chainOwners.RemoveAt(i);
                chainRootBoneNames.RemoveAt(i);
                chainBones.RemoveAt(i);
                chainInitialRotations.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        void CollectChainDescendants(Transform parent, HashSet<Transform> boneSet,
            Dictionary<string, SpringBoneParamsDTO> paramsByBone, SpringBoneJointConfig inheritedConfig)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!boneSet.Contains(child)) continue;

                SpringBoneJointConfig c;
                if (paramsByBone.TryGetValue(child.name, out var childParams))
                {
                    // Tagged as its own root: independent chain — let outer loop handle it.
                    if (childParams.isRoot) continue;
                    // Non-root entry: use its own params for this joint.
                    c = BuildConfigFromDTO(childParams, child.localRotation);
                }
                else
                {
                    c = inheritedConfig;
                    c.LocalRotation = child.localRotation;
                }

                chainJoints.Add(child);
                chainConfigs.Add(c);
                CollectChainDescendants(child, boneSet, paramsByBone, inheritedConfig);
            }
        }

        void FlushChain(GameObject owner, Transform rootParent, string rootBoneName)
        {
            for (int j = 0; j < chainJoints.Count; j++)
            {
                var c = chainConfigs[j];
                Transform tail = j + 1 < chainJoints.Count ? chainJoints[j + 1] : null;

                if (tail != null)
                {
                    float3 localPos = (float3)tail.localPosition;
                    float3 scale = (float3)tail.lossyScale;
                    float3 scaledPos = localPos * scale;
                    float len = math.length(scaledPos);
                    c.BoneAxis = len > 0.0001f ? scaledPos / len : new float3(0, 1, 0);
                    c.Length = len;
                }
                else
                {
                    c.BoneAxis = new float3(0, 1, 0);
                    c.Length = 0.1f;
                }

                chainConfigs[j] = c;
            }

            var tails = new float3[chainJoints.Count];
            for (int j = 0; j < chainJoints.Count; j++)
                tails[j] = j + 1 < chainJoints.Count ? (float3)chainJoints[j + 1].position : (float3)chainJoints[j].position;

            var jointsCopy = chainJoints.ToArray();
            var initRots = new Quaternion[jointsCopy.Length];
            for (int j = 0; j < jointsCopy.Length; j++) initRots[j] = jointsCopy[j].localRotation;

            int slot = service.RegisterSpring(jointsCopy, chainConfigs.ToArray(), tails);
            slotIndices.Add(slot);
            chainRootParents.Add(rootParent);
            chainOwners.Add(owner);
            chainRootBoneNames.Add(rootBoneName);
            chainBones.Add(jointsCopy);
            chainInitialRotations.Add(initRots);
        }

        static SpringBoneJointConfig BuildConfigFromDTO(SpringBoneParamsDTO p, Quaternion initialRotation)
        {
            float3 gravityDir = p.gravityDir != null && p.gravityDir.Length == 3
                ? new float3(p.gravityDir[0], p.gravityDir[1], p.gravityDir[2])
                : new float3(0, -1, 0);
            return new SpringBoneJointConfig
            {
                Stiffness = p.stiffness,
                Drag = p.drag,
                GravityDir = gravityDir,
                GravityPower = p.gravityPower,
                LocalRotation = initialRotation,
            };
        }

        void LateUpdate()
        {
            if (service == null || slotIndices.Count == 0) return;

            for (int i = 0; i < slotIndices.Count; i++)
            {
                var p = chainRootParents[i];
                if (p == null) continue;
                service.SetParentData(slotIndices[i], p.rotation, p.localToWorldMatrix);
            }

            service.Simulate(Time.deltaTime);
        }
    }
}
