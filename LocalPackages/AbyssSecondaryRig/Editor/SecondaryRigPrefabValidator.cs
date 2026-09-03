#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig.Editor
{
    internal sealed class SecondaryRigValidationResult
    {
        public int Errors;
        public int Warnings;
        public int Infos;
        public readonly List<string> Lines = new();

        public bool IsValid => Errors == 0;

        public string BuildReport()
        {
            Lines.Insert(0, $"Production Validation: Errors={Errors}, Warnings={Warnings}, Info={Infos}");
            return string.Join("\n", Lines);
        }

        public void Error(string message) { Errors++; Lines.Add("ERROR: " + message); }
        public void Warn(string message) { Warnings++; Lines.Add("WARN: " + message); }
        public void Info(string message) { Infos++; Lines.Add("INFO: " + message); }
    }

    internal static class SecondaryRigPrefabValidator
    {
        public static SecondaryRigValidationResult Validate(GameObject root)
        {
            SecondaryRigValidationResult result = new();
            if (root == null)
            {
                result.Error("Character root is null.");
                return result;
            }

            SecondaryRigController controller = root.GetComponent<SecondaryRigController>() ??
                                               root.GetComponentInChildren<SecondaryRigController>(true);
            if (controller == null)
            {
                result.Error("SecondaryRigController is missing.");
                return result;
            }

            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
                result.Error("Animator is missing.");
            else if (!animator.isHuman)
                result.Warn("Animator is not configured as Humanoid. Auto collider and Humanoid mirror mapping will be limited.");
            else
                result.Info("Humanoid Animator: OK");

            if (controller.PresetLibrary == null)
                result.Error("Preset Library is not assigned.");
            else
            {
                string presetReport = controller.PresetLibrary.ValidateLibrary();
                if (presetReport == "Preset Library: OK") result.Info(presetReport);
                else result.Warn(presetReport);
            }

            controller.RefreshColliders();
            bool bindingValid = controller.ValidateBindings(out string bindingReport);
            if (bindingValid) result.Info(bindingReport);
            else result.Error(bindingReport);

            Transform simulationRoot = controller.SimulationRoot;
            if (simulationRoot == null)
                result.Error("Simulation Root is missing.");

            Transform colliderRoot = controller.ColliderRoot;
            if (colliderRoot == null)
                result.Error("Collider Root is missing.");
            else if (controller.ColliderCount == 0)
                result.Warn("No Secondary Rig body colliders were found under Collider Root.");
            else
                result.Info($"Body Colliders: {controller.ColliderCount}");

            HashSet<int> springRoots = new();
            IReadOnlyList<SecondaryRigChainBinding> chains = controller.Chains;
            for (int i = 0; i < chains.Count; i++)
            {
                SecondaryRigChainBinding chain = chains[i];
                if (chain == null || !chain.HasUsableBones())
                    continue;

                if (!springRoots.Add(chain.SpringRoot.GetInstanceID()))
                    result.Error($"Duplicate Spring Root: {chain.SpringRoot.name}");

                if (!chain.SpringRoot.IsChildOf(root.transform) || !chain.TargetRoot.IsChildOf(root.transform))
                    result.Error($"Chain '{chain.StableKey}' references transforms outside the character root.");

                if (controller.PresetLibrary != null && controller.PresetLibrary.Find(chain.PresetName) == null)
                    result.Warn($"Preset '{chain.PresetName}' is not in the library; fallback settings will be used.");
            }

            SecondaryRigCollider[] colliders = colliderRoot != null
                ? colliderRoot.GetComponentsInChildren<SecondaryRigCollider>(true)
                : Array.Empty<SecondaryRigCollider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                SecondaryRigCollider collider = colliders[i];
                if (collider is SecondaryRigSphereCollider sphere && sphere.Radius <= 0f)
                    result.Warn($"Sphere collider '{sphere.name}' has zero radius.");
                if (collider is SecondaryRigCapsuleCollider capsule && capsule.Radius <= 0f)
                    result.Warn($"Capsule collider '{capsule.name}' has zero radius.");

                SecondaryRigColliderFollower follower = collider.GetComponent<SecondaryRigColliderFollower>();
                if (follower != null && follower.FollowTarget == null)
                    result.Warn($"Collider '{collider.name}' has a Follower with no Follow Target.");
            }

            SecondaryRigLodSettings lod = controller.LodSettings;
            if (lod != null && lod.Enabled)
            {
                if (lod.FullDistance > lod.ReducedDistance || lod.ReducedDistance > lod.DisableDistance)
                    result.Error("LOD distances are not ordered Full <= Reduced <= Disable.");
                else
                    result.Info($"LOD: Full {lod.FullDistance:0.#}m / Reduced {lod.ReducedDistance:0.#}m / Disable {lod.DisableDistance:0.#}m");
            }

            SecondaryRigStressTest stress = root.GetComponentInChildren<SecondaryRigStressTest>(true);
            if (stress != null)
                result.Info("Stress Test component is available for QA.");

            return result;
        }
    }
}
#endif
