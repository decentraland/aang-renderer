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
        }

        public void RegisterAll(IEnumerable<(GameObject owner, SpringBoneData[] springs)> wearableSprings)
        {
            UnregisterAll();
            foreach (var (owner, springs) in wearableSprings)
            {
                if (springs == null || springs.Length == 0) continue;
                RegisterWearable(owner, springs);
            }
            Debug.Log($"[SpringBones] driver registered {slotIndices.Count} chain(s)");
        }

        void RegisterWearable(GameObject owner, SpringBoneData[] springs)
        {
            chainJoints.Clear();
            chainConfigs.Clear();
            Transform chainRootParent = null;
            string chainRootBoneName = null;

            for (int i = 0; i < springs.Length; i++)
            {
                var sb = springs[i];

                if (sb.IsRoot && chainJoints.Count > 0)
                {
                    FlushChain(owner, chainRootParent, chainRootBoneName);
                    chainJoints.Clear();
                    chainConfigs.Clear();
                }

                sb.ManagedTransform.localRotation = sb.InitialLocalRotation;

                if (sb.IsRoot)
                {
                    chainRootParent = sb.ManagedTransform.parent;
                    chainRootBoneName = sb.ManagedTransform.name;
                }

                chainJoints.Add(sb.ManagedTransform);
                chainConfigs.Add(BuildJointConfig(sb));
            }

            if (chainJoints.Count > 0)
                FlushChain(owner, chainRootParent, chainRootBoneName);
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

            int slot = service.RegisterSpring(chainJoints.ToArray(), chainConfigs.ToArray(), tails);
            slotIndices.Add(slot);
            chainRootParents.Add(rootParent);
            chainOwners.Add(owner);
            chainRootBoneNames.Add(rootBoneName);
        }

        /// <summary>
        /// Override physics params per chain. Keys in <paramref name="paramsByBone"/> match the
        /// chain's root bone name. Returns number of chains updated.
        /// </summary>
        public int UpdateParamsForWearable(GameObject owner, Dictionary<string, SpringBoneParamsDTO> paramsByBone)
        {
            if (service == null || owner == null || paramsByBone == null) return 0;

            int updated = 0;
            for (int i = 0; i < slotIndices.Count; i++)
            {
                if (chainOwners[i] != owner) continue;
                if (!paramsByBone.TryGetValue(chainRootBoneNames[i], out var p)) continue;

                float3 gravityDir = p.gravityDir != null && p.gravityDir.Length == 3
                    ? new float3(p.gravityDir[0], p.gravityDir[1], p.gravityDir[2])
                    : new float3(0, -1, 0);

                service.UpdateSlotParams(slotIndices[i], p.stiffness, p.drag, gravityDir, p.gravityPower);
                updated++;
            }
            return updated;
        }

        static SpringBoneJointConfig BuildJointConfig(SpringBoneData d) => new()
        {
            Stiffness = d.Stiffness,
            Drag = d.Drag,
            GravityDir = d.GravityDir,
            GravityPower = d.GravityPower,
            LocalRotation = d.InitialLocalRotation,
        };

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
