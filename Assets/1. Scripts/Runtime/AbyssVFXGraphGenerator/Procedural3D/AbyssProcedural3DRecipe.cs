using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.VFX.Procedural3D
{
    [Serializable]
    public class Procedural3DRecipe
    {
        public string name = "Procedural3DVFX";
        public string representationMode = "Procedural3D";
        public string backend = "Procedural3D";
        public string notes = "";
        public string attachTarget = "Root";
        public bool loop = true;
        public bool useWorldSpace = false;
        public int randomSeed = 0;
        public float duration = 2.0f;
        public float characterHeight = 1.8f;
        public float globalScale = 1.0f;
        public ColorData baseColor = new ColorData(1f, 0.65f, 0.18f, 0.45f);
        public ColorData edgeColor = new ColorData(1f, 0.95f, 0.70f, 1f);
        public float emission = 4.5f;
        public List<Procedural3DElementRecipe> elements = new List<Procedural3DElementRecipe>();

        public Color BaseColor => baseColor.ToColor();
        public Color EdgeColor => edgeColor.ToColor();
    }

    [Serializable]
    public class Procedural3DElementRecipe
    {
        public string id = "element";
        public string type = "Core"; // Core, AuraShell, Ring, Motes, Streaks, Beam
        public string meshType = "Auto";
        public string orbitAxis = "Y";
        public int count = 1;
        public bool verticalFade = false;
        public Vector3Data localPosition = new Vector3Data(0, 0, 0);
        public Vector3Data localRotation = new Vector3Data(0, 0, 0);
        public Vector3Data localScale = new Vector3Data(1, 1, 1);
        public float radius = 0.1f;
        public float thickness = 0.03f;
        public float height = 1.0f;
        public float width = 0.1f;
        public float orbitRadius = 0.4f;
        public float speed = 1.0f;
        public float secondarySpeed = 0.0f;
        public float pulseSpeed = 2.0f;
        public float pulseAmount = 0.08f;
        public float noiseFrequency = 4.0f;
        public float noiseAmplitude = 0.04f;
        public float flowSpeed = 0.5f;
        public float alpha = 0.35f;
        public float fresnelPower = 4.0f;
        public float colorMultiplier = 1.0f;
    }

    [Serializable]
    public struct Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public struct ColorData
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public ColorData(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color ToColor() => new Color(r, g, b, a);
    }
}
