using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "Garden/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        public List<SoundEntry> entries = new();

        [Serializable]
        public class SoundEntry
        {
            public string key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0.8f, 1.2f)] public float pitchMin = 1f;
            [Range(0.8f, 1.2f)] public float pitchMax = 1f;
        }
    }
}
