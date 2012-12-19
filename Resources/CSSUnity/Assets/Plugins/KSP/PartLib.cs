using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PartToolsLib
{
    public enum FileType : int
    {
        ModelBinary = 76543
    }

    /// <summary>
    /// Type of entry in the file format
    /// </summary>
    public enum EntryType : int
    {
        ChildTransformStart,
        ChildTransformEnd,

        Animation,

        MeshCollider,
        SphereCollider,
        CapsuleCollider,
        BoxCollider,

        MeshFilter,
        MeshRenderer,
        SkinnedMeshRenderer,

        Materials,
        Material,
        Textures,

        MeshStart,
        MeshVerts,
        MeshUV,
        MeshUV2,
        MeshNormals,
        MeshTangents,
        MeshTriangles,
        MeshBoneWeights,
        MeshBindPoses,
        MeshEnd,

        Light,

        TagAndLayer,

        MeshCollider2,
        SphereCollider2,
        CapsuleCollider2,
        BoxCollider2,
        WheelCollider
    }

    /// <summary>
    /// Supported shaders
    /// </summary>
    public enum ShaderType : int
    {
        Custom,
        Diffuse,
        Specular,
        Bumped,
        BumpedSpecular,
        Emissive,
        EmissiveSpecular,
        EmissiveBumpedSpecular,
        AlphaCutout,
        AlphaCutoutBumped
    }

    /// <summary>
    /// Supported animation types
    /// </summary>
    public enum AnimationType : int
    {
        Transform,
        Material,
        Light,
        AudioSource
    }

    /// <summary>
    /// Supported texture types
    /// </summary>
    public enum TextureType : int
    {
        Texture,
        NormalMap
    }
}