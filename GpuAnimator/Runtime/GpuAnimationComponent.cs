using PureMVCFramework.Advantages;
using PureMVCFramework.Entity;
using System;
using UnityEngine;

//namespace GpuSkinning
//{
    public class GpuAnimation : IComponent, IRecycleable
    {
        public class AnimationClip
        {
            public GpuSkinningAnimClip clip;

            public int currentFrame = 0;
            public int lastFrame = 0;
            public bool completed;
            public bool loop;

            public Action<Entity, object> callback;
            public object userdata;
        }

        public GpuSkinningAnimClip[] clips;
        public MaterialPropertyBlock prop;
        internal Renderer renderer;
        public AnimationClip current = new AnimationClip();

        public float duration = 0.03f;
        public float timer = 0;


        public void OnRecycle()
        {
            clips = null;
            prop = null;
            renderer = null;
            current = new AnimationClip();
            duration = 0;
            timer = 0;
        }
    }

//}
