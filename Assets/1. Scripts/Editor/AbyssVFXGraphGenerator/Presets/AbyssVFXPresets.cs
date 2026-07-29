#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Editor.VFXAI
{
    internal enum AbyssVFXPreset
    {
        ImpactBurst,
        ProjectileTrail,
        Aura,
        Smoke,
        GroundMagic,
        CompositeSlashImpact
    }

    internal static class AbyssVFXPresets
    {
        internal static AbyssVFXRecipe Create(AbyssVFXPreset preset)
        {
            return preset switch
            {
                AbyssVFXPreset.ImpactBurst => ImpactBurst(),
                AbyssVFXPreset.ProjectileTrail => ProjectileTrail(),
                AbyssVFXPreset.Aura => Aura(),
                AbyssVFXPreset.Smoke => Smoke(),
                AbyssVFXPreset.GroundMagic => GroundMagic(),
                AbyssVFXPreset.CompositeSlashImpact => CompositeSlashImpact(),
                _ => ImpactBurst()
            };
        }

        internal static AbyssVFXPreset GuessFromPrompt(string prompt)
        {
            string text = (prompt ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(text, "연기", "smoke", "fog", "안개")) return AbyssVFXPreset.Smoke;
            if (ContainsAny(text, "오라", "aura", "buff", "버프")) return AbyssVFXPreset.Aura;
            if (ContainsAny(text, "투사체", "projectile", "bullet", "탄환")) return AbyssVFXPreset.ProjectileTrail;
            if (ContainsAny(text, "마법진", "ground", "circle", "ring", "장판")) return AbyssVFXPreset.GroundMagic;
            if (ContainsAny(text, "검기", "slash", "베기")) return AbyssVFXPreset.CompositeSlashImpact;
            return AbyssVFXPreset.ImpactBurst;
        }

        private static AbyssVFXRecipe ImpactBurst()
        {
            return new AbyssVFXRecipe
            {
                name = "VFX_Impact_Burst_AI",
                description = "짧고 강한 타격 파편",
                systems = new List<AbyssVFXSystemRecipe>
                {
                    new()
                    {
                        name = "Impact Sparks",
                        spawnMode = "Burst",
                        capacity = 512,
                        spawnCount = 72,
                        lifetime = new AbyssFloatRange(0.22f, 0.65f),
                        size = new AbyssFloatRange(0.025f, 0.12f),
                        positionMode = "Box",
                        spawnBoxExtents = new AbyssVector3(0.04f, 0.04f, 0.04f),
                        velocityMode = "Radial",
                        speed = new AbyssFloatRange(2.5f, 8f),
                        gravity = new AbyssVector3(0f, -5f, 0f),
                        drag = 1.1f,
                        startColor = AbyssColor.From(new Color(1f, 0.08f, 0.015f, 1f)),
                        blendMode = "Additive"
                    }
                }
            };
        }

        private static AbyssVFXRecipe ProjectileTrail()
        {
            return new AbyssVFXRecipe
            {
                name = "VFX_Projectile_Trail_AI",
                description = "이동하는 오브젝트에 붙이는 짧은 파티클 꼬리",
                systems = new List<AbyssVFXSystemRecipe>
                {
                    new()
                    {
                        name = "Trail Motes",
                        spawnMode = "Rate",
                        capacity = 1024,
                        spawnRate = 55,
                        lifetime = new AbyssFloatRange(0.18f, 0.42f),
                        size = new AbyssFloatRange(0.04f, 0.13f),
                        positionMode = "Box",
                        spawnBoxExtents = new AbyssVector3(0.04f, 0.04f, 0.04f),
                        velocityMode = "Directional",
                        direction = new AbyssVector3(0f, 0.15f, -1f),
                        speed = new AbyssFloatRange(0.2f, 1.2f),
                        spread = 0.25f,
                        gravity = new AbyssVector3(0f, 0f, 0f),
                        drag = 0.8f,
                        startColor = AbyssColor.From(new Color(0.25f, 0.02f, 0.8f, 1f)),
                        blendMode = "Additive"
                    }
                }
            };
        }

        private static AbyssVFXRecipe Aura()
        {
            return new AbyssVFXRecipe
            {
                name = "VFX_Aura_AI",
                description = "캐릭터 주변에서 위로 떠오르는 오라",
                systems = new List<AbyssVFXSystemRecipe>
                {
                    new()
                    {
                        name = "Aura Motes",
                        spawnMode = "Rate",
                        capacity = 2048,
                        spawnRate = 30,
                        lifetime = new AbyssFloatRange(0.7f, 1.6f),
                        size = new AbyssFloatRange(0.045f, 0.18f),
                        positionMode = "Box",
                        spawnBoxExtents = new AbyssVector3(0.45f, 0.75f, 0.45f),
                        velocityMode = "Directional",
                        direction = new AbyssVector3(0f, 1f, 0f),
                        speed = new AbyssFloatRange(0.15f, 0.9f),
                        spread = 0.3f,
                        gravity = new AbyssVector3(0f, 0.15f, 0f),
                        drag = 0.35f,
                        startColor = AbyssColor.From(new Color(0.35f, 0.02f, 0.75f, 0.75f)),
                        blendMode = "Additive"
                    }
                }
            };
        }

        private static AbyssVFXRecipe Smoke()
        {
            return new AbyssVFXRecipe
            {
                name = "VFX_Smoke_AI",
                description = "천천히 위로 퍼지는 연기 초안",
                systems = new List<AbyssVFXSystemRecipe>
                {
                    new()
                    {
                        name = "Smoke Puffs",
                        spawnMode = "Rate",
                        capacity = 1024,
                        spawnRate = 14,
                        lifetime = new AbyssFloatRange(1.2f, 2.8f),
                        size = new AbyssFloatRange(0.16f, 0.5f),
                        positionMode = "Box",
                        spawnBoxExtents = new AbyssVector3(0.12f, 0.04f, 0.12f),
                        velocityMode = "Directional",
                        direction = new AbyssVector3(0f, 1f, 0f),
                        speed = new AbyssFloatRange(0.15f, 0.75f),
                        spread = 0.3f,
                        gravity = new AbyssVector3(0f, 0.12f, 0f),
                        drag = 0.45f,
                        startColor = AbyssColor.From(new Color(0.12f, 0.12f, 0.14f, 0.5f)),
                        blendMode = "Alpha"
                    }
                }
            };
        }

        private static AbyssVFXRecipe GroundMagic()
        {
            return new AbyssVFXRecipe
            {
                name = "VFX_Ground_Magic_AI",
                description = "바닥 근처에 넓게 발생하는 마법 입자",
                systems = new List<AbyssVFXSystemRecipe>
                {
                    new()
                    {
                        name = "Ground Motes",
                        spawnMode = "Rate",
                        capacity = 2048,
                        spawnRate = 45,
                        lifetime = new AbyssFloatRange(0.5f, 1.25f),
                        size = new AbyssFloatRange(0.025f, 0.11f),
                        positionMode = "Box",
                        spawnBoxExtents = new AbyssVector3(1.1f, 0.02f, 1.1f),
                        velocityMode = "Directional",
                        direction = new AbyssVector3(0f, 1f, 0f),
                        speed = new AbyssFloatRange(0.05f, 0.45f),
                        spread = 0.2f,
                        gravity = new AbyssVector3(0f, 0.05f, 0f),
                        drag = 0.25f,
                        startColor = AbyssColor.From(new Color(0.05f, 0.45f, 1f, 0.9f)),
                        blendMode = "Additive"
                    }
                }
            };
        }

        private static AbyssVFXRecipe CompositeSlashImpact()
        {
            AbyssVFXRecipe recipe = ImpactBurst();
            recipe.name = "VFX_Slash_Impact_Composite_AI";
            recipe.description = "검기 타격용 파편 + 잔류 연무";
            recipe.systems.Add(new AbyssVFXSystemRecipe
            {
                name = "Dark Residue",
                spawnMode = "Burst",
                capacity = 256,
                spawnCount = 22,
                lifetime = new AbyssFloatRange(0.5f, 1.3f),
                size = new AbyssFloatRange(0.12f, 0.35f),
                positionMode = "Box",
                spawnBoxExtents = new AbyssVector3(0.15f, 0.08f, 0.15f),
                velocityMode = "Directional",
                direction = new AbyssVector3(0f, 1f, 0f),
                speed = new AbyssFloatRange(0.15f, 0.8f),
                spread = 0.4f,
                gravity = new AbyssVector3(0f, 0.08f, 0f),
                drag = 0.5f,
                startColor = AbyssColor.From(new Color(0.04f, 0.01f, 0.08f, 0.62f)),
                blendMode = "Alpha"
            });
            return recipe;
        }

        private static bool ContainsAny(string text, params string[] words)
        {
            foreach (string word in words)
            {
                if (text.Contains(word))
                    return true;
            }
            return false;
        }
    }
}
#endif
