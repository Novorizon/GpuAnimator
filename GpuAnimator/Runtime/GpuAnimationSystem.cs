using PureMVCFramework.Entity;
using System;
using UnityEngine;

public class GpuAnimationSystem : ComponentSystem<GpuAnimation>
{
    protected override void OnUpdate(int index, Entity entity, GpuAnimation component)
    {
        GpuAnimation.AnimationClip current = component.current;
        if (current == null || current.clip == null || current.completed ||
            component.duration <= 0 || float.IsNaN(component.duration) || float.IsInfinity(component.duration))
            return;

        component.timer += Time.deltaTime;
        double framesToAdvance = Math.Floor(component.timer / component.duration);
        if (framesToAdvance < 1)
            return;

        component.timer = Math.Max(0, (float)(component.timer - framesToAdvance * component.duration));
        current.lastFrame = current.currentFrame;
        GpuSkinningAnimClip clip = current.clip;
        double nextFrame = current.currentFrame + framesToAdvance;

        if (current.loop)
        {
            int frameCount = clip.Length();
            current.currentFrame = clip.startFrame + (int)((nextFrame - clip.startFrame) % frameCount);
        }
        else if (nextFrame > clip.endFrame)
        {
            current.currentFrame = clip.endFrame;
            current.completed = true;
            component.timer = 0;
        }
        else
        {
            current.currentFrame = (int)nextFrame;
        }

        GpuAnimator.ApplyFrame(entity, component);

        // Commit completion before invoking user code, which may start another animation.
        if (current.completed)
            current.callback?.Invoke(entity, current.userdata);
    }
}
