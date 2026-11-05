using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Game.Framework;
using GpuAnimatorSamples;
using UnityEngine;
class Program
{
    static int passed;
    static void Assert(bool c){if(!c)throw new Exception("Assertion failed");}
    static void Test(string name,Action check){check();passed++;Console.WriteLine("PASS "+name);}
    static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Expected rejection");}
    static AnimationData Data()=>new AnimationData{totalFrame=5,totalBoneNum=1,clips=new[]{new AnimationClipData("loop",0,2,true,2),new AnimationClipData("once",3,4,false,2)}};
    static int Main()
    {
        try
        {
            Test("loop and once sampling endpoints",()=>{Assert(GpuAnimationBakeUtility.CalculateFrameCount(1,30,true)==30);Assert(GpuAnimationBakeUtility.CalculateFrameCount(1,30,false)==31);Assert(GpuAnimationBakeUtility.GetSampleTime(30,1,30,false)==1);});
            Test("short clips and invalid sampling",()=>{Assert(GpuAnimationBakeUtility.CalculateFrameCount(.001f,30,true)==1);Reject(()=>GpuAnimationBakeUtility.CalculateFrameCount(1,0,true));Reject(()=>GpuAnimationBakeUtility.CalculateFrameCount(float.NaN,30,false));});
            Test("texture capacity and overflow",()=>{GpuAnimationBakeUtility.CalculateTextureSize(73,317,1024,out int w,out int h);Assert((long)w*h>=73L*317*8);Reject(()=>GpuAnimationBakeUtility.CalculateTextureSize(int.MaxValue,int.MaxValue,2048,out _,out _));});
            Test("encoded values round-trip through shader byte fractions",()=>{foreach(float value in new[]{-127.5f,-2f,0f,.5f,127.999f}){var b=GpuAnimationBakeUtility.EncodeFloat(value);double decoded=(b.r%128+b.g/256.0+b.b/65536.0+b.a/16777216.0)*(b.r>=128?-1:1);Assert(Math.Abs(decoded-value)<.00001);}});
            Test("encoding rejects nonfinite and out-of-range",()=>{Reject(()=>GpuAnimationBakeUtility.EncodeFloat(128));Reject(()=>GpuAnimationBakeUtility.EncodeFloat(float.NaN));});
            Test("uniform signed scale decomposes",()=>{foreach(float s in new[]{-2f,.5f,0f,2f}){var m=Matrix4x4.identity;m.m00=m.m11=m.m22=s;m.m03=3;GpuAnimationBakeUtility.DecomposeMatrix(m,out Quaternion q,out Vector3 p,out float scale);Assert(Math.Abs(scale-s)<.0001 && p.x==3 && Math.Abs(q.w-1)<.0001);}});
            Test("nonuniform and shear are rejected",()=>{var m=Matrix4x4.identity;m.m00=2;Reject(()=>GpuAnimationBakeUtility.DecomposeMatrix(m,out _,out _,out _));m=Matrix4x4.identity;m.m01=.2f;Reject(()=>GpuAnimationBakeUtility.DecomposeMatrix(m,out _,out _,out _));});
            Test("transformed bounds contain shifted box",()=>{var m=Matrix4x4.identity;m.m03=5;var b=GpuAnimationBakeUtility.TransformBounds(new Bounds(Vector3.zero,new Vector3(2,2,2)),m);Assert(b.center.x==5 && b.extents.x>=1);});
            Test("baked metadata decodes once for shared injection",()=>{using var stream=new MemoryStream();new BinaryFormatter().Serialize(stream,Data());var data=AnimationData.FromBytes(stream.ToArray());Assert(data.clips.Length==2 && data.totalFrame==5);});
            var go=new GameObject("player");var renderer=go.AddComponent<Renderer>();var player=go.AddComponent<GpuAnimator>();var shared=Data();
            Test("initialize injects caller assets without auto-play",()=>Assert(player.Initialize(renderer,shared) && !player.IsPlaying));
            Test("loop tick and material property preservation",()=>{int key=99;var block=new MaterialPropertyBlock();block.SetInt(key,7);renderer.SetPropertyBlock(block);Assert(player.TryPlay("loop"));player.Tick(2);renderer.GetPropertyBlock(block);Assert(player.CurrentFrame==1 && block.GetInt(key)==7);});
            Test("one shot finishes once and can restart in callback",()=>{int completed=0;player.TryPlay("once",(p,u)=>{completed++;p.TryPlay("loop");},loop:false);player.Tick(3);Assert(completed==1 && player.IsPlaying && player.CurrentClipName=="loop");});
            Test("invalid delta and clip do not corrupt playback",()=>{int frame=player.CurrentFrame;player.Tick(float.NaN);Assert(!player.TryPlay("missing") && player.CurrentFrame==frame);});
            Test("clear releases state and suppresses completion",()=>{int completed=0;player.TryPlay("once",(p,u)=>completed++,loop:false);player.Clear();player.Tick(3);Assert(!player.IsInitialized && completed==0 && player.CurrentClipName==null);});
            Test("shared data is unaffected by clip overrides",()=>{player.Initialize(renderer,shared);player.TryPlay("loop",loop:false);Assert(shared.clips[0].loop);});
            UnityThread.InitializeManual();
            Test("Framework pool recycles cleared playback and reuses instance",()=>{using var pool=new GpuAnimatorPool(go,shared,1);var first=pool.Spawn("loop",Vector3.zero,Quaternion.identity);pool.Recycle(first);Assert(!first.IsInitialized && !first.IsPlaying);var second=pool.Spawn("once",Vector3.zero,Quaternion.identity);Assert(ReferenceEquals(first,second) && pool.ActiveCount==1);pool.Recycle(second);});
            Test("Framework pool disposes active players and rejects invalid clips",()=>{var pool=new GpuAnimatorPool(go,shared);Reject(()=>pool.Spawn("bad",Vector3.zero,Quaternion.identity));Assert(pool.ActiveCount==0);var active=pool.Spawn("loop",Vector3.zero,Quaternion.identity);pool.Dispose();Assert(!active.IsInitialized && pool.ActiveCount==0);});
            UnityThread.Reset();Console.WriteLine("PASS "+passed+" GPU source checks; native bake/import/rendering not executed.");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
