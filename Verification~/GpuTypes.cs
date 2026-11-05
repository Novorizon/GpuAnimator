using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Renderer : MonoBehaviour
    {
        private MaterialPropertyBlock values = new MaterialPropertyBlock();
        public void GetPropertyBlock(MaterialPropertyBlock block) { block.Copy(values); }
        public void SetPropertyBlock(MaterialPropertyBlock block) { values.Copy(block); }
    }
    public class MaterialPropertyBlock
    {
        readonly Dictionary<int,int> values = new Dictionary<int,int>();
        public void SetInt(int id,int value) { values[id]=value; }
        public int GetInt(int id) => values.TryGetValue(id,out int value)?value:0;
        public void Copy(MaterialPropertyBlock source) { values.Clear(); foreach(var p in source.values)values[p.Key]=p.Value; }
    }
    public static class Shader { public static int PropertyToID(string name)=>name.GetHashCode(); }
    public static class Random { public static int Range(int min,int max)=>min; }
    public partial struct Vector3
    {
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float f)=>new Vector3(a.x*f,a.y*f,a.z*f);
    }
    public struct Color32 { public byte r,g,b,a; public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;} }
    public struct Bounds
    {
        public Vector3 center,extents;
        public Bounds(Vector3 center,Vector3 size){this.center=center;extents=size*.5f;}
    }
    public struct Matrix4x4
    {
        public float m00,m10,m20,m30,m01,m11,m21,m31,m02,m12,m22,m32,m03,m13,m23,m33;
        public float this[int i] { get { switch(i){case 0:return m00;case 1:return m10;case 2:return m20;case 3:return m30;case 4:return m01;case 5:return m11;case 6:return m21;case 7:return m31;case 8:return m02;case 9:return m12;case 10:return m22;case 11:return m32;case 12:return m03;case 13:return m13;case 14:return m23;default:return m33;} } }
        public static Matrix4x4 identity=>new Matrix4x4{m00=1,m11=1,m22=1,m33=1};
    }
}
