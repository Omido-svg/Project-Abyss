using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [Serializable]
    public sealed class WeaponEffectSlot
    {
        [SerializeField] private string eventId = "fire";
        [SerializeField] private WeaponPoint point;
        [SerializeField] private GameObject prefab;
        [SerializeField] private ParticleSystem particle;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clip;
        [SerializeField, Min(0f)] private float destroySpawnedAfter = 5f;
        public string EventId => eventId;
        public WeaponPoint Point => point;
        public GameObject Prefab => prefab;
        public ParticleSystem Particle => particle;
        public AudioSource AudioSource => audioSource;
        public AudioClip Clip => clip;
        public float DestroySpawnedAfter => destroySpawnedAfter;
    }

    [DisallowMultipleComponent]
    public sealed class WeaponEffectModule : WeaponModule
    {
        [SerializeField] private List<WeaponEffectSlot> slots = new();

        public bool Play(string eventId) => Play(eventId, null, null);

        public bool Play(string eventId, Vector3? worldPosition, Quaternion? worldRotation)
        {
            bool played = false;
            for (int i = 0; i < slots.Count; i++)
            {
                WeaponEffectSlot slot = slots[i];
                if (slot == null || !string.Equals(slot.EventId, eventId, StringComparison.Ordinal)) continue;
                Transform point = slot.Point != null ? slot.Point.transform : transform;
                Vector3 position = worldPosition ?? point.position;
                Quaternion rotation = worldRotation ?? point.rotation;

                if (slot.Particle != null) { slot.Particle.Play(true); played = true; }
                if (slot.AudioSource != null && slot.Clip != null) { slot.AudioSource.PlayOneShot(slot.Clip); played = true; }
                if (slot.Prefab != null)
                {
                    GameObject clone = Instantiate(slot.Prefab, position, rotation);
                    if (slot.DestroySpawnedAfter > 0f) Destroy(clone, slot.DestroySpawnedAfter);
                    played = true;
                }
            }
            return played;
        }
    }
}
