using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.VFX.Procedural3D
{
    [ExecuteAlways]
    public sealed class AbyssProcedural3DController : MonoBehaviour
    {
        public float characterHeight = 1.8f;
        public float globalScale = 1.0f;
        public Color baseColor = new Color(1f, 0.65f, 0.18f, 0.45f);
        public Color edgeColor = new Color(1f, 0.95f, 0.70f, 1f);
        public float emission = 4.5f;
        public Material sharedMaterial;
        public List<Procedural3DElementRecipe> elements = new List<Procedural3DElementRecipe>();

        [Serializable]
        private sealed class RuntimeElement
        {
            public Procedural3DElementRecipe Recipe;
            public List<Transform> Instances = new List<Transform>();
            public List<float> Phases = new List<float>();
            public List<float> Angles = new List<float>();
            public List<float> Speeds = new List<float>();
            public List<float> Radii = new List<float>();
        }

        private readonly List<RuntimeElement> _runtime = new List<RuntimeElement>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int NoiseFrequencyId = Shader.PropertyToID("_NoiseFrequency");
        private static readonly int NoiseAmplitudeId = Shader.PropertyToID("_NoiseAmplitude");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int VerticalFadeId = Shader.PropertyToID("_VerticalFade");
        private static readonly int FresnelPowerId = Shader.PropertyToID("_FresnelPower");

        public void ApplyRecipeJson(string json)
        {
            Procedural3DRecipe recipe = JsonUtility.FromJson<Procedural3DRecipe>(json);
            if (recipe == null) return;
            characterHeight = recipe.characterHeight;
            globalScale = recipe.globalScale;
            baseColor = recipe.BaseColor;
            edgeColor = recipe.EdgeColor;
            emission = recipe.emission;
            elements = recipe.elements ?? new List<Procedural3DElementRecipe>();
        }

        private void OnEnable() => Rebuild();
        private void OnValidate()
        {
            if (enabled) Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Cleanup();
            if (sharedMaterial == null) return;
            for (int i = 0; i < elements.Count; i++)
            {
                RuntimeElement runtime = new RuntimeElement { Recipe = elements[i] };
                BuildElement(runtime);
                _runtime.Add(runtime);
            }
        }

        private void Update()
        {
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            for (int i = 0; i < _runtime.Count; i++)
            {
                RuntimeElement runtime = _runtime[i];
                string type = runtime.Recipe.type ?? string.Empty;
                if (type.Equals("Core", StringComparison.OrdinalIgnoreCase)) UpdateCore(runtime, time);
                else if (type.Equals("AuraShell", StringComparison.OrdinalIgnoreCase)) UpdateAura(runtime, time);
                else if (type.Equals("Ring", StringComparison.OrdinalIgnoreCase)) UpdateRings(runtime, time);
                else if (type.Equals("Motes", StringComparison.OrdinalIgnoreCase)) UpdateMotes(runtime, time);
                else if (type.Equals("Streaks", StringComparison.OrdinalIgnoreCase)) UpdateStreaks(runtime, time);
                else if (type.Equals("Beam", StringComparison.OrdinalIgnoreCase)) UpdateBeams(runtime, time);
            }
        }

        private void UpdateCore(RuntimeElement runtime, float time)
        {
            for (int j = 0; j < runtime.Instances.Count; j++)
            {
                Transform t = runtime.Instances[j];
                float pulse = 1f + Mathf.Sin(time * runtime.Recipe.pulseSpeed + runtime.Phases[j]) * runtime.Recipe.pulseAmount;
                t.localScale = runtime.Recipe.localScale.ToVector3() * (runtime.Recipe.radius * 2f * globalScale) * pulse;
            }
        }

        private void UpdateAura(RuntimeElement runtime, float time)
        {
            for (int j = 0; j < runtime.Instances.Count; j++)
            {
                Transform t = runtime.Instances[j];
                float pulse = 1f + Mathf.Sin(time * runtime.Recipe.pulseSpeed + runtime.Phases[j]) * runtime.Recipe.pulseAmount * 0.5f;
                Vector3 baseScale = Vector3.Scale(runtime.Recipe.localScale.ToVector3(), new Vector3(characterHeight, characterHeight, characterHeight)) * globalScale;
                t.localScale = baseScale * pulse;
            }
        }

        private void UpdateRings(RuntimeElement runtime, float time)
        {
            for (int j = 0; j < runtime.Instances.Count; j++)
            {
                Transform t = runtime.Instances[j];
                Vector3 rot = runtime.Recipe.localRotation.ToVector3();
                t.localRotation = Quaternion.Euler(
                    rot.x + Mathf.Sin(time * 0.8f + runtime.Phases[j]) * 12f,
                    rot.y + time * runtime.Recipe.speed * runtime.Speeds[j] * 40f,
                    rot.z + Mathf.Cos(time * 0.9f + runtime.Phases[j]) * 10f);
            }
        }

        private void UpdateMotes(RuntimeElement runtime, float time)
        {
            for (int j = 0; j < runtime.Instances.Count; j++)
            {
                Transform t = runtime.Instances[j];
                float p = Mathf.Repeat(time * runtime.Recipe.speed * 0.25f + runtime.Phases[j], 1f);
                float angle = runtime.Angles[j] + time * runtime.Speeds[j];
                Vector3 local = runtime.Recipe.localPosition.ToVector3();
                if ((runtime.Recipe.orbitAxis ?? "Y").Equals("X", StringComparison.OrdinalIgnoreCase))
                {
                    local.y += Mathf.Cos(angle) * runtime.Radii[j];
                    local.z += Mathf.Sin(angle) * runtime.Radii[j];
                    local.x += -runtime.Recipe.height * 0.5f + p * runtime.Recipe.height;
                }
                else
                {
                    local.x += Mathf.Cos(angle) * runtime.Radii[j];
                    local.z += Mathf.Sin(angle) * runtime.Radii[j];
                    local.y += -runtime.Recipe.height * 0.5f + p * runtime.Recipe.height;
                }
                t.localPosition = local * globalScale;
                t.localScale = Vector3.one * (runtime.Recipe.radius * 0.35f * globalScale + Mathf.Sin(time * 4f + runtime.Phases[j]) * runtime.Recipe.radius * 0.08f * globalScale);
            }
        }

        private void UpdateStreaks(RuntimeElement runtime, float time)
        {
            for (int j = 0; j < runtime.Instances.Count; j++)
            {
                Transform t = runtime.Instances[j];
                float pulse = 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(time * runtime.Recipe.pulseSpeed + runtime.Phases[j]));
                Vector3 local = runtime.Recipe.localPosition.ToVector3();
                float angle = runtime.Angles[j];
                local.x += Mathf.Cos(angle) * runtime.Radii[j];
                local.z += Mathf.Sin(angle) * runtime.Radii[j];
                t.localPosition = local * globalScale;
                t.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                t.localScale = new Vector3(runtime.Recipe.radius * 0.45f, runtime.Recipe.height * pulse, runtime.Recipe.radius * 0.45f) * globalScale;
            }
        }

        private void UpdateBeams(RuntimeElement runtime, float time)
        {
            for (int j = 0; j < runtime.Instances.Count; j++)
            {
                Transform t = runtime.Instances[j];
                float pulse = 1f + Mathf.Sin(time * runtime.Recipe.pulseSpeed + runtime.Phases[j]) * runtime.Recipe.pulseAmount;
                t.localPosition = runtime.Recipe.localPosition.ToVector3() * globalScale;
                t.localRotation = Quaternion.Euler(runtime.Recipe.localRotation.ToVector3());
                t.localScale = new Vector3(runtime.Recipe.width, runtime.Recipe.height * pulse, runtime.Recipe.width) * globalScale;
            }
        }

        private void BuildElement(RuntimeElement runtime)
        {
            string type = runtime.Recipe.type ?? string.Empty;
            if (type.Equals("Core", StringComparison.OrdinalIgnoreCase))
            {
                Transform t = CreateRenderable(runtime.Recipe.id, PrimitiveType.Sphere, transform);
                t.localPosition = runtime.Recipe.localPosition.ToVector3() * globalScale;
                ApplyMaterialBlock(t, runtime.Recipe);
                runtime.Instances.Add(t);
                runtime.Phases.Add(UnityEngine.Random.value * Mathf.PI * 2f);
            }
            else if (type.Equals("AuraShell", StringComparison.OrdinalIgnoreCase))
            {
                Transform t = CreateRenderable(runtime.Recipe.id, PrimitiveType.Capsule, transform);
                t.localPosition = runtime.Recipe.localPosition.ToVector3() * globalScale;
                ApplyMaterialBlock(t, runtime.Recipe);
                runtime.Instances.Add(t);
                runtime.Phases.Add(UnityEngine.Random.value * Mathf.PI * 2f);
            }
            else if (type.Equals("Ring", StringComparison.OrdinalIgnoreCase))
            {
                Mesh torus = AbyssProceduralMeshFactory.CreateTorus(runtime.Recipe.radius * globalScale, runtime.Recipe.thickness * globalScale, 64, 18);
                Transform t = CreateRenderable(runtime.Recipe.id, torus, transform);
                t.localPosition = runtime.Recipe.localPosition.ToVector3() * globalScale;
                t.localScale = runtime.Recipe.localScale.ToVector3();
                ApplyMaterialBlock(t, runtime.Recipe);
                runtime.Instances.Add(t);
                runtime.Phases.Add(UnityEngine.Random.value * Mathf.PI * 2f);
                runtime.Speeds.Add(UnityEngine.Random.Range(0.85f, 1.15f));
            }
            else if (type.Equals("Motes", StringComparison.OrdinalIgnoreCase))
            {
                Mesh sphere = AbyssProceduralMeshFactory.GetPrimitiveMesh(PrimitiveType.Sphere);
                int count = Mathf.Max(1, runtime.Recipe.count);
                for (int i = 0; i < count; i++)
                {
                    Transform t = CreateRenderable(runtime.Recipe.id + "_" + i.ToString("00"), sphere, transform);
                    ApplyMaterialBlock(t, runtime.Recipe);
                    runtime.Instances.Add(t);
                    runtime.Phases.Add(i / (float)count + UnityEngine.Random.value);
                    runtime.Angles.Add(i / (float)count * Mathf.PI * 2f);
                    runtime.Speeds.Add(UnityEngine.Random.Range(-1.2f, 1.2f) * runtime.Recipe.speed);
                    runtime.Radii.Add(runtime.Recipe.orbitRadius * UnityEngine.Random.Range(0.55f, 1f));
                }
            }
            else if (type.Equals("Streaks", StringComparison.OrdinalIgnoreCase))
            {
                Mesh cylinder = AbyssProceduralMeshFactory.GetPrimitiveMesh(PrimitiveType.Cylinder);
                int count = Mathf.Max(1, runtime.Recipe.count);
                for (int i = 0; i < count; i++)
                {
                    Transform t = CreateRenderable(runtime.Recipe.id + "_" + i.ToString("00"), cylinder, transform);
                    ApplyMaterialBlock(t, runtime.Recipe);
                    runtime.Instances.Add(t);
                    runtime.Phases.Add(i / (float)count * Mathf.PI * 2f);
                    runtime.Angles.Add(i / (float)count * Mathf.PI * 2f);
                    runtime.Radii.Add(runtime.Recipe.orbitRadius);
                }
            }
            else if (type.Equals("Beam", StringComparison.OrdinalIgnoreCase))
            {
                Transform t = CreateRenderable(runtime.Recipe.id, PrimitiveType.Cylinder, transform);
                ApplyMaterialBlock(t, runtime.Recipe);
                runtime.Instances.Add(t);
                runtime.Phases.Add(UnityEngine.Random.value * Mathf.PI * 2f);
            }
        }

        private Transform CreateRenderable(string name, PrimitiveType primitiveType, Transform parent)
        {
            return CreateRenderable(name, AbyssProceduralMeshFactory.GetPrimitiveMesh(primitiveType), parent);
        }

        private Transform CreateRenderable(string name, Mesh mesh, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }

        private void ApplyMaterialBlock(Transform target, Procedural3DElementRecipe recipe)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null) return;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, baseColor * recipe.colorMultiplier);
            block.SetColor(EdgeColorId, edgeColor * recipe.colorMultiplier);
            block.SetFloat(EmissionId, emission * recipe.colorMultiplier);
            block.SetFloat(PulseSpeedId, recipe.pulseSpeed);
            block.SetFloat(PulseAmountId, recipe.pulseAmount);
            block.SetFloat(NoiseFrequencyId, recipe.noiseFrequency);
            block.SetFloat(NoiseAmplitudeId, recipe.noiseAmplitude);
            block.SetFloat(FlowSpeedId, recipe.flowSpeed);
            block.SetFloat(AlphaId, recipe.alpha);
            block.SetFloat(VerticalFadeId, recipe.verticalFade ? 1f : 0f);
            block.SetFloat(FresnelPowerId, recipe.fresnelPower);
            renderer.SetPropertyBlock(block);
        }

        private void Cleanup()
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in transform) children.Add(child.gameObject);
            for (int i = 0; i < children.Count; i++)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(children[i]);
                else Destroy(children[i]);
#else
                Destroy(children[i]);
#endif
            }
            _runtime.Clear();
        }
    }
}
