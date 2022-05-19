using PureMVCFramework.Entity;
using UnityEngine;

public class GpuAnimationSystem : ComponentSystem<GpuAnimation>
{
    protected override void OnUpdate(int index, Entity entity, GpuAnimation component)
    {
        if (component.current.clip != null)
        {
            component.timer += Time.deltaTime;
            if (component.timer >= component.duration)
            {
                component.current.currentFrame++;
                component.timer -= component.duration;

                if (component.current.currentFrame > component.current.clip.endFrame - 1)
                {
                    if (component.current.clip.loop)
                    {
                        component.current.currentFrame = component.current.clip.startFrame;
                    }
                    else
                    {
                        component.current.currentFrame = component.current.clip.endFrame;
                        component.current.callback?.Invoke(entity, component.current.userdata);
                    }
                }

                component.prop.SetInt("_BlendFrameIndex", component.current.lastFrame);
                component.prop.SetInt("_FrameIndex", component.current.currentFrame);
                component.current.lastFrame = component.current.currentFrame;

                if (entity.gameObject)
                    entity.gameObject.GetComponent<Renderer>().SetPropertyBlock(component.prop);

            }
        }
    }
}
