using UnityEngine;

namespace MscDuo.Sync
{
    // gestos do jogador: o jogo liga um objeto da mão na câmera enquanto o gesto dura, e as vozes saem dos grupos do MasterAudio
    internal static class Gestures
    {
        internal const byte MiddleFinger = 1;
        internal const byte Punch = 2;
        internal const byte Wave = 4;
        internal const byte Smoke = 8;
        internal const byte Drink = 16;
        internal const byte Pee = 32;

        static readonly string[] Hands = { "MiddleFinger", "Fist", "Hello", "Smoking", "Drink/Hand" };
        static readonly byte[] HandFlags = { MiddleFinger, Punch, Wave, Smoke, Drink };
        static readonly string[] VoiceGroups = { "Swearing", "Fuck" };

        static Transform camera;
        static readonly Transform[] hands = new Transform[Hands.Length];
        static Transform pee;
        static AudioSource[][] voices;
        static bool[][] playing;
        static byte lastVoice;
        static byte voiceCount;

        internal static byte Capture()
        {
            var main = Camera.main;
            if (main == null) return 0;
            if (camera != main.transform)
            {
                camera = main.transform;
                for (int i = 0; i < Hands.Length; i++) hands[i] = camera.Find(Hands[i]);
                pee = camera.parent != null ? camera.parent.Find("Piss/Fluid") : null;
            }
            byte flags = 0;
            for (int i = 0; i < hands.Length; i++)
                if (hands[i] != null && hands[i].gameObject.activeInHierarchy) flags |= HandFlags[i];
            if (pee != null && pee.gameObject.activeInHierarchy) flags |= Pee;
            return flags;
        }

        // voz nova: id = grupo * 16 + variação, e o contador avisa o outro lado que ela começou de novo
        internal static void CaptureVoice(out byte voice, out byte count)
        {
            if (voices == null) FindVoices();
            for (int g = 0; g < voices.Length; g++)
                for (int v = 0; v < voices[g].Length; v++)
                {
                    var now = voices[g][v] != null && voices[g][v].isPlaying;
                    if (now && !playing[g][v])
                    {
                        lastVoice = (byte)((g + 1) * 16 + v);
                        voiceCount++;
                    }
                    playing[g][v] = now;
                }
            voice = lastVoice;
            count = voiceCount;
        }

        internal static AudioClip VoiceClip(byte voice)
        {
            if (voices == null) FindVoices();
            var g = voice / 16 - 1;
            var v = voice % 16;
            if (g < 0 || g >= voices.Length || v >= voices[g].Length || voices[g][v] == null) return null;
            return voices[g][v].clip;
        }

        static void FindVoices()
        {
            var master = GameObject.Find("MasterAudio");
            voices = new AudioSource[VoiceGroups.Length][];
            playing = new bool[VoiceGroups.Length][];
            for (int g = 0; g < VoiceGroups.Length; g++)
            {
                var group = master != null ? master.transform.Find(VoiceGroups[g]) : null;
                voices[g] = group != null ? group.GetComponentsInChildren<AudioSource>(true) : new AudioSource[0];
                playing[g] = new bool[voices[g].Length];
            }
        }
    }
}
