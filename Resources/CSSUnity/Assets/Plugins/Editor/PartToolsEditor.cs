using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[CustomEditor(typeof(PartTools))]
public class PartToolsEditor : Editor
{
    public PartTools Target { get { return (PartTools)target; } }

    private static GUILayoutOption colLabel = GUILayout.Width(100);

    public override void OnInspectorGUI()
    {
        DrawWriterGUI();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
    }

    private void DrawWriterGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Part Name", colLabel);
        Target.modelName = GUILayout.TextField(Target.modelName);
        GUILayout.EndHorizontal();

        EditorGUILayout.Separator();

        GUILayout.BeginHorizontal();
        GUILayout.Label("File Path", colLabel);
        Target.filePath = GUILayout.TextField(Target.filePath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Set"))
        {
            string folderPath = EditorUtility.OpenFolderPanel("Select part path", Target.filePath, "");

            if (folderPath != null && folderPath != "")
            {
                Target.filePath = folderPath;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("File Name", colLabel);
        Target.filename = GUILayout.TextField(Target.filename);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Copy Textures", colLabel);
        Target.copyTexturesToOutputDirectory = GUILayout.Toggle(Target.copyTexturesToOutputDirectory, "");
        GUILayout.EndHorizontal();

        if (Target.copyTexturesToOutputDirectory)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Convert Textures", colLabel);
            Target.convertTextures = GUILayout.Toggle(Target.convertTextures, "");
            GUILayout.EndHorizontal();

            if (!Target.convertTextures)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Rename Textures", colLabel);
                Target.autoRenameTextures = GUILayout.Toggle(Target.autoRenameTextures, "");
                GUILayout.EndHorizontal();
            }
        }

        if (GUILayout.Button("Write"))
        {
            PartWriter.Write(Target.modelName,
                Target.filePath, Target.filename, ".mu",
                Target.transform,
                Target.copyTexturesToOutputDirectory, Target.convertTextures, Target.autoRenameTextures);
        }

        if (GUILayout.Button("Write All"))
        {
            WriteAll();
        }
    }

    [MenuItem("KSP/PartTools/Write All")]
    static void WriteAll()
    {
        if (EditorUtility.DisplayDialog(
            "This action will parse entire project structure and export all PartTools prefabs to their defined paths.\n\nOnly prefabs updated to the project will be exported, scene-only objects will be ignored.\n\n",
            "Are you sure you want to do this?",
            "Yes",
            "No"))
        {
            RecurseDirectory(new DirectoryInfo(Application.dataPath));
        }
    }

    private static string GetAssetName(FileInfo file)
    {
        return "Assets/" + file.FullName.Substring(Application.dataPath.Length + 1);
    }

    private static void RecurseDirectory(DirectoryInfo dir)
    {
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            PartTools pt = (PartTools)AssetDatabase.LoadAssetAtPath(GetAssetName(file), typeof(PartTools));

            if (pt == null)
                continue;

            PrefabType prefabType = PrefabUtility.GetPrefabType(pt);
            if (prefabType == PrefabType.Prefab)
            {
                Debug.Log("Writing " + pt.modelName);

                PartToolsEditor.PartWriter.Write(
                pt.modelName,
                pt.filePath, pt.filename, ".mu",
                pt.transform,
                pt.copyTexturesToOutputDirectory, pt.convertTextures, pt.autoRenameTextures);
            }
        }

        DirectoryInfo[] subDirs = dir.GetDirectories();
        foreach (DirectoryInfo subDir in subDirs)
        {
            RecurseDirectory(subDir);
        }
    }

    /// <summary>
    /// Contains the methods to write mu files
    /// </summary>
    public static class PartWriter
    {
        public static int fileVersion = 0;

        /// <summary>
        /// Writes a gameobject heirarchy to a mu file
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <param name="partName">Name of eventual part name</param>
        /// <param name="rootObject">Parent representing world origin</param>
        public static void Write(string modelName, string filePath, string filename, string fileExtension, Transform rootObject)
        {
            Write(modelName, filePath, filename, fileExtension, rootObject, false, false, false);
        }

        /// <summary>
        /// Writes a gameobject heirarchy to a mu file and can copy/rename textures
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <param name="target">Target gameobject to write</param>
        /// <param name="copyTexturesToTargetDir">Copy textures to target directory</param>
        /// <param name="renameTextures">Rename textures with respect to target name</param>
        public static void Write(string modelName, string filePath, string filename, string fileExtension, Transform target, bool copyTexturesToTargetDir, bool convertTextures, bool renameTextures)
        {
            PartWriter.copyTexturesToTargetDir = copyTexturesToTargetDir;
            PartWriter.convertTextures = convertTextures;
            PartWriter.renameTextures = renameTextures;
            PartWriter.filePath = filePath;
            PartWriter.filename = filename;
            PartWriter.fileExtension = fileExtension;

            materials = new List<Material>();
            textures = new TextureDummyList();

            System.IO.FileInfo file = new System.IO.FileInfo(filePath);
            file.Directory.Create();

            BinaryWriter bw = new BinaryWriter(File.Open(filePath + filename + fileExtension, FileMode.Create));

            // write header
            bw.Write((int)PartToolsLib.FileType.ModelBinary);
            bw.Write(fileVersion);
            bw.Write(modelName);

            try
            {
                WriteChild(bw, target);

                // write shared materials
                if (materials.Count > 0)
                {
                    bw.Write((int)PartToolsLib.EntryType.Materials);
                    bw.Write(materials.Count);
                    foreach (Material mat in materials)
                    {
                        WriteMaterial(bw, mat);
                        bw.Flush();
                    }

                    // write textures
                    if (textures.Count > 0)
                    {
                        WriteTextures(bw);
                        bw.Flush();
                    }
                }

            }
            catch (System.Exception ex)
            {
                Debug.LogError("File error: " + ex.Message + "\n" + ex.StackTrace + "\n");
            }

            Debug.Log(filePath + filename + fileExtension + " written.");
            bw.Close();

            materials = null;
            textures = null;
        }

        #region Write methods

        const string textureFileExtension = "mbm";

        static bool copyTexturesToTargetDir;
        static bool convertTextures;
        static bool renameTextures;
        static string filePath;
        static string filename;
        static string fileExtension;

        // materials array is used to cache all the shared materials before writing
        static List<Material> materials;

        public class TextureDummy
        {
            public Texture texture;

            public PartToolsLib.TextureType type;

            public TextureDummy(Texture texture, PartToolsLib.TextureType type)
            {
                this.texture = texture;
                this.type = type;
            }
        }

        public class TextureDummyList : List<TextureDummy>
        {
            public bool Contains(Texture tex)
            {
                foreach (TextureDummy dummy in textures)
                {
                    if (dummy.texture == tex)
                        return true;
                }
                return false;
            }

            public void Add(Texture tex, PartToolsLib.TextureType type)
            {
                if (!Contains(tex))
                {
                    Add(new TextureDummy(tex, type));
                }
            }

            public int IndexOf(Texture tex)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].texture == tex)
                        return i;
                }
                return -1;
            }
        }

        static TextureDummyList textures;


        static void WriteChild(BinaryWriter bw, Transform t)
        {
            WriteTransform(bw, t);

            WriteTagAndLayer(bw, t);
            WriteCollider(bw, t);
            WriteMeshFiler(bw, t);
            WriteMeshRenderer(bw, t);
            WriteSkinnedMeshRenderer(bw, t);
            WriteAnimation(bw, t);
            WriteLight(bw, t);

            foreach (Transform child in t)
            {
                bw.Write((int)PartToolsLib.EntryType.ChildTransformStart);
                WriteChild(bw, child);
                bw.Write((int)PartToolsLib.EntryType.ChildTransformEnd);
            }

            bw.Flush();
        }


        static void WriteMeshFiler(BinaryWriter bw, Transform t)
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshFilter);
                WriteMesh(bw, mf.sharedMesh);
            }
        }

        static void WriteMeshRenderer(BinaryWriter bw, Transform t)
        {
            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshRenderer);

                bw.Write(mr.sharedMaterials.Length);
                for (int i = 0; i < mr.sharedMaterials.Length; i++)
                {
                    if (!materials.Contains(mr.sharedMaterials[i]))
                        materials.Add(mr.sharedMaterials[i]);
                    bw.Write(materials.IndexOf(mr.sharedMaterials[i]));
                }
            }
        }

        static void WriteSkinnedMeshRenderer(BinaryWriter bw, Transform t)
        {
            SkinnedMeshRenderer smr = t.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                bw.Write((int)PartToolsLib.EntryType.SkinnedMeshRenderer);

                bw.Write(smr.sharedMaterials.Length);
                for (int i = 0; i < smr.sharedMaterials.Length; i++)
                {
                    if (!materials.Contains(smr.sharedMaterials[i]))
                        materials.Add(smr.sharedMaterials[i]);
                    bw.Write(materials.IndexOf(smr.sharedMaterials[i]));
                }

                bw.Write(smr.localBounds.center.x);
                bw.Write(smr.localBounds.center.y);
                bw.Write(smr.localBounds.center.z);
                bw.Write(smr.localBounds.size.x);
                bw.Write(smr.localBounds.size.y);
                bw.Write(smr.localBounds.size.z);

                bw.Write((int)smr.quality);

                bw.Write(smr.updateWhenOffscreen);

                int nBones = smr.bones.Length;
                bw.Write(nBones);
                for (int i = 0; i < nBones; i++)
                {
                    bw.Write(smr.bones[i].gameObject.name);
                }

                WriteMesh(bw, smr.sharedMesh);
            }
        }


        static void WriteMaterial(BinaryWriter bw, Material mat)
        {
            bw.Write(mat.name);
            Debug.Log(mat.shader.name);
            switch (mat.shader.name)
            {
                case "KSP/Specular":

                    bw.Write((int)PartToolsLib.ShaderType.Specular);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);

                    WriteColor(bw, mat.GetColor("_SpecColor"));
                    bw.Write(mat.GetFloat("_Shininess"));

                    break;

                case "KSP/Bumped":

                    bw.Write((int)PartToolsLib.ShaderType.Bumped);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);
                    WriteMaterialTexture(bw, mat, "_BumpMap", PartToolsLib.TextureType.NormalMap);

                    break;

                case "KSP/Bumped Specular":
                    bw.Write((int)PartToolsLib.ShaderType.BumpedSpecular);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);
                    WriteMaterialTexture(bw, mat, "_BumpMap", PartToolsLib.TextureType.NormalMap);

                    WriteColor(bw, mat.GetColor("_SpecColor"));
                    bw.Write(mat.GetFloat("_Shininess"));

                    break;

                case "KSP/Emissive/Diffuse":
                    bw.Write((int)PartToolsLib.ShaderType.Emissive);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);

                    WriteMaterialTexture(bw, mat, "_Emissive", PartToolsLib.TextureType.Texture);
                    WriteColor(bw, mat.GetColor("_EmissiveColor"));

                    break;

                case "KSP/Emissive/Specular":
                    bw.Write((int)PartToolsLib.ShaderType.EmissiveSpecular);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);

                    WriteColor(bw, mat.GetColor("_SpecColor"));
                    bw.Write(mat.GetFloat("_Shininess"));

                    WriteMaterialTexture(bw, mat, "_Emissive", PartToolsLib.TextureType.Texture);
                    WriteColor(bw, mat.GetColor("_EmissiveColor"));

                    break;

                case "KSP/Emissive/Bumped Specular":
                    bw.Write((int)PartToolsLib.ShaderType.EmissiveBumpedSpecular);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);
                    WriteMaterialTexture(bw, mat, "_BumpMap", PartToolsLib.TextureType.NormalMap);

                    WriteColor(bw, mat.GetColor("_SpecColor"));
                    bw.Write(mat.GetFloat("_Shininess"));

                    WriteMaterialTexture(bw, mat, "_Emissive", PartToolsLib.TextureType.Texture);
                    WriteColor(bw, mat.GetColor("_EmissiveColor"));

                    break;

                case "KSP/Alpha/Cutoff":
                    bw.Write((int)PartToolsLib.ShaderType.AlphaCutout);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);

                    bw.Write(mat.GetFloat("_Cutoff"));

                    break;

                case "KSP/Alpha/Cutoff Bumped":
                    bw.Write((int)PartToolsLib.ShaderType.AlphaCutoutBumped);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);
                    WriteMaterialTexture(bw, mat, "_BumpMap", PartToolsLib.TextureType.NormalMap);

                    bw.Write(mat.GetFloat("_Cutoff"));

                    break;

                case "Diffuse":
                default:
                    bw.Write((int)PartToolsLib.ShaderType.Diffuse);

                    WriteMaterialTexture(bw, mat, "_MainTex", PartToolsLib.TextureType.Texture);

                    break;
            }
        }

        static void WriteMaterialTexture(BinaryWriter bw, Material mat, string textureName, PartToolsLib.TextureType type)
        {
            AddTextureInstance(bw, mat, textureName, type);

            Vector2 tempV2 = mat.GetTextureScale(textureName);
            bw.Write(tempV2.x);
            bw.Write(tempV2.y);

            tempV2 = mat.GetTextureOffset(textureName);
            bw.Write(tempV2.x);
            bw.Write(tempV2.y);
        }

        static void AddTextureInstance(BinaryWriter bw, Material mat, string textureName, PartToolsLib.TextureType type)
        {
            Texture tex = mat.GetTexture(textureName);

            if (tex != null)
            {
                if (!textures.Contains(tex))
                    textures.Add(tex, type);

                bw.Write(textures.IndexOf(tex));
            }
            else
            {
                bw.Write(-1);
            }
        }

        static void WriteTextures(BinaryWriter bw)
        {
            bw.Write((int)PartToolsLib.EntryType.Textures);
            bw.Write(textures.Count);

            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] == null)
                {
                    Debug.LogError(i);

                    bw.Write(" ");
                    bw.Write((int)PartToolsLib.TextureType.Texture);
                }
                else
                {
                    string name = textures[i].texture.name;

                    if (copyTexturesToTargetDir)
                    {

                        string path = AssetDatabase.GetAssetPath(textures[i].texture);
                        string texExt = (path.Substring(path.LastIndexOf('.') + 1)).ToLower();

                        if (convertTextures)
                        {
                            name = filename + i.ToString("D3") + "." + textureFileExtension;

                            Debug.Log("Texture: '" + path + "' >> '" + name + "'");
                            BitmapWriter.Write2D(textures[i].texture, filePath + name, textures[i].type);
                        }
                        else
                        {
                            if (renameTextures)
                                name = filename + i.ToString("D3") + "." + texExt;
                            else
                                name = name + "." + texExt;

                            Debug.Log("Texture: '" + path + "' >> '" + name + "'");
                            AssetDatabase.CopyAsset(path, filePath + name);

                            if (textures[i].type == PartToolsLib.TextureType.NormalMap)
                            {
                                // check if we need to create a normal map from the texture
                                TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                                if (textureImporter.convertToNormalmap)
                                {
                                    Debug.Log("Converting '" + (filePath + name) + "' to a normal map");
                                    // we do. make it so
                                    ConvertNormalTexture(filePath + name, textureImporter.heightmapScale);
                                }
                            }
                        }
                    }

                    bw.Write(name);
                    bw.Write((int)textures[i].type);
                }
            }
        }


        static void WriteCollider(BinaryWriter bw, Transform t)
        {
            MeshCollider mc = t.GetComponent<MeshCollider>();
            if (mc != null)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshCollider2);
                bw.Write(mc.isTrigger);
                bw.Write(mc.convex);
                WriteMesh(bw, mc.sharedMesh);
                return;
            }

            BoxCollider bc = t.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bw.Write((int)PartToolsLib.EntryType.BoxCollider2);
                bw.Write(bc.isTrigger);
                bw.Write(bc.size.x);
                bw.Write(bc.size.y);
                bw.Write(bc.size.z);
                bw.Write(bc.center.x);
                bw.Write(bc.center.y);
                bw.Write(bc.center.z);
                return;
            }

            CapsuleCollider cc = t.GetComponent<CapsuleCollider>();
            if (cc != null)
            {
                bw.Write((int)PartToolsLib.EntryType.CapsuleCollider2);
                bw.Write(cc.isTrigger);
                bw.Write(cc.radius);
                bw.Write(cc.height);
                bw.Write(cc.direction);
                bw.Write(cc.center.x);
                bw.Write(cc.center.y);
                bw.Write(cc.center.z);
                return;
            }

            SphereCollider sc = t.GetComponent<SphereCollider>();
            if (sc != null)
            {
                bw.Write((int)PartToolsLib.EntryType.SphereCollider2);
                bw.Write(sc.isTrigger);
                bw.Write(sc.radius);
                bw.Write(sc.center.x);
                bw.Write(sc.center.y);
                bw.Write(sc.center.z);
                return;
            }

            WheelCollider wc = t.GetComponent<WheelCollider>();
            if (wc != null)
            {
                bw.Write((int)PartToolsLib.EntryType.WheelCollider);

                bw.Write(wc.mass);
                bw.Write(wc.radius);
                bw.Write(wc.suspensionDistance);

                bw.Write(wc.center.x);
                bw.Write(wc.center.y);
                bw.Write(wc.center.z);

                bw.Write(wc.suspensionSpring.spring);
                bw.Write(wc.suspensionSpring.damper);
                bw.Write(wc.suspensionSpring.targetPosition);

                bw.Write(wc.forwardFriction.extremumSlip);
                bw.Write(wc.forwardFriction.extremumValue);
                bw.Write(wc.forwardFriction.asymptoteSlip);
                bw.Write(wc.forwardFriction.asymptoteValue);
                bw.Write(wc.forwardFriction.stiffness);

                bw.Write(wc.sidewaysFriction.extremumSlip);
                bw.Write(wc.sidewaysFriction.extremumValue);
                bw.Write(wc.sidewaysFriction.asymptoteSlip);
                bw.Write(wc.sidewaysFriction.asymptoteValue);
                bw.Write(wc.sidewaysFriction.stiffness);
                return;
            }
        }

        static void WriteMesh(BinaryWriter bw, Mesh mesh)
        {
            int vCount = mesh.vertexCount;
            int smCount = mesh.subMeshCount;


            bw.Write((int)PartToolsLib.EntryType.MeshStart);
            bw.Write(vCount);
            bw.Write(smCount);


            Vector3[] verts = mesh.vertices;
            bw.Write((int)PartToolsLib.EntryType.MeshVerts);
            foreach (Vector3 v in verts)
            {
                bw.Write(v.x);
                bw.Write(v.y);
                bw.Write(v.z);
            }

            Vector2[] uvs = mesh.uv;
            if (uvs != null && uvs.Length == vCount)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshUV);
                foreach (Vector2 uv in uvs)
                {
                    bw.Write(uv.x);
                    bw.Write(uv.y);
                }
            }

            Vector2[] uv2s = mesh.uv2;
            if (uv2s != null && uv2s.Length == vCount)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshUV2);
                foreach (Vector2 uv in uv2s)
                {
                    bw.Write(uv.x);
                    bw.Write(uv.y);
                }
            }

            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length == vCount)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshNormals);
                foreach (Vector3 n in normals)
                {
                    bw.Write(n.x);
                    bw.Write(n.y);
                    bw.Write(n.z);
                }
            }

            Vector4[] tangents = mesh.tangents;
            if (tangents != null && tangents.Length == vCount)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshTangents);
                foreach (Vector4 ta in tangents)
                {
                    bw.Write(ta.x);
                    bw.Write(ta.y);
                    bw.Write(ta.z);
                    bw.Write(ta.w);
                }
            }

            BoneWeight[] weights = mesh.boneWeights;
            if (weights != null && weights.Length == vCount)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshBoneWeights);
                foreach (BoneWeight w in weights)
                {
                    bw.Write(w.boneIndex0);
                    bw.Write(w.weight0);
                    bw.Write(w.boneIndex1);
                    bw.Write(w.weight1);
                    bw.Write(w.boneIndex2);
                    bw.Write(w.weight2);
                    bw.Write(w.boneIndex3);
                    bw.Write(w.weight3);
                }
            }

            Matrix4x4[] bindposes = mesh.bindposes;
            if (bindposes != null && bindposes.Length > 0)
            {
                bw.Write((int)PartToolsLib.EntryType.MeshBindPoses);
                bw.Write(bindposes.Length);
                foreach (Matrix4x4 m in bindposes)
                {
                    bw.Write(m.m00);
                    bw.Write(m.m01);
                    bw.Write(m.m02);
                    bw.Write(m.m03);

                    bw.Write(m.m10);
                    bw.Write(m.m11);
                    bw.Write(m.m12);
                    bw.Write(m.m13);

                    bw.Write(m.m20);
                    bw.Write(m.m21);
                    bw.Write(m.m22);
                    bw.Write(m.m23);

                    bw.Write(m.m30);
                    bw.Write(m.m31);
                    bw.Write(m.m32);
                    bw.Write(m.m33);
                }
            }

            int[] tri;
            for (int i = 0; i < smCount; i++)
            {
                tri = mesh.GetTriangles(i);

                bw.Write((int)PartToolsLib.EntryType.MeshTriangles);
                bw.Write(tri.Length);

                foreach (int tr in tri)
                {
                    bw.Write(tr);
                }
            }

            bw.Write((int)PartToolsLib.EntryType.MeshEnd);
        }

        static void WriteTransform(BinaryWriter bw, Transform t)
        {
            bw.Write(t.gameObject.name);

            bw.Write(t.localPosition.x);
            bw.Write(t.localPosition.y);
            bw.Write(t.localPosition.z);
            bw.Write(t.localRotation.x);
            bw.Write(t.localRotation.y);
            bw.Write(t.localRotation.z);
            bw.Write(t.localRotation.w);
            bw.Write(t.localScale.x);
            bw.Write(t.localScale.y);
            bw.Write(t.localScale.z);
            bw.Write(t.localScale.x);
        }

        static void WriteTagAndLayer(BinaryWriter bw, Transform t)
        {
            bw.Write((int)PartToolsLib.EntryType.TagAndLayer);
            bw.Write(t.tag);
            bw.Write(t.gameObject.layer);
        }

        static void WriteAnimation(BinaryWriter bw, Transform t)
        {
            Animation anim = t.GetComponent<Animation>();
            if (anim != null)
            {
                int clipCount = anim.GetClipCount();
                if (clipCount == 0) // if no clips then can return
                    return;

                bw.Write((int)PartToolsLib.EntryType.Animation);



                // sadly need to use animationutility to get clip/curve data (hence writer must be an editor script)
                AnimationClip[] clips = AnimationUtility.GetAnimationClips(anim);
                bw.Write(clipCount);
                foreach (AnimationClip clip in clips)
                {
                    bw.Write(clip.name);

                    bw.Write(clip.localBounds.center.x);
                    bw.Write(clip.localBounds.center.y);
                    bw.Write(clip.localBounds.center.z);
                    bw.Write(clip.localBounds.size.x);
                    bw.Write(clip.localBounds.size.y);
                    bw.Write(clip.localBounds.size.z);

                    bw.Write((int)clip.wrapMode);


                    AnimationClipCurveData[] curveDatas = AnimationUtility.GetAllCurves(clip, true);
                    int curvesN = curveDatas.Length;

                    bw.Write((int)curvesN);
                    for (int i = 0; i < curvesN; i++)
                    {
                        bw.Write(curveDatas[i].path);
                        bw.Write(curveDatas[i].propertyName);

                        // work out type and write key
                        if (curveDatas[i].type == typeof(Transform))
                        {
                            bw.Write((int)PartToolsLib.AnimationType.Transform);
                        }
                        else if (curveDatas[i].type == typeof(Material))
                        {
                            bw.Write((int)PartToolsLib.AnimationType.Material);
                        }
                        else if (curveDatas[i].type == typeof(Light))
                        {
                            bw.Write((int)PartToolsLib.AnimationType.Light);
                        }
                        else if (curveDatas[i].type == typeof(AudioSource))
                        {
                            bw.Write((int)PartToolsLib.AnimationType.AudioSource);
                        }

                        bw.Write((int)curveDatas[i].curve.preWrapMode);
                        bw.Write((int)curveDatas[i].curve.postWrapMode);

                        // curve keys
                        int keysN = curveDatas[i].curve.keys.Length;
                        bw.Write(keysN);
                        for (int j = 0; j < keysN; j++)
                        {
                            bw.Write(curveDatas[i].curve.keys[j].time);
                            bw.Write(curveDatas[i].curve.keys[j].value);
                            bw.Write(curveDatas[i].curve.keys[j].inTangent);
                            bw.Write(curveDatas[i].curve.keys[j].outTangent);
                            bw.Write(curveDatas[i].curve.keys[j].tangentMode);
                        }
                    }
                }

                if (anim.clip != null)
                    bw.Write(anim.clip.name);
                else
                    bw.Write((string)"");

                bw.Write(anim.playAutomatically);
            }
        }

        static void WriteLight(BinaryWriter bw, Transform t)
        {
            Light l = t.GetComponent<Light>();

            if (l != null)
            {
                bw.Write((int)PartToolsLib.EntryType.Light);

                bw.Write((int)l.type);
                bw.Write(l.intensity);
                bw.Write(l.range);
                WriteColor(bw, l.color);
                bw.Write(l.cullingMask);
            }
        }


        static void WriteColor(BinaryWriter bw, Color c)
        {
            bw.Write(c.r);
            bw.Write(c.g);
            bw.Write(c.b);
            bw.Write(c.a);
        }

        #endregion

        #region Convert texture to normal map

        static void ConvertNormalTexture(string fileFullPath, float normalStrength)
        {
            byte[] imageData = File.ReadAllBytes(fileFullPath);
            Texture2D tex = new Texture2D(1, 1);
            tex.LoadImage(imageData);

            int width = tex.width;
            int height = tex.height;

            Texture2D newTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            newTex.wrapMode = TextureWrapMode.Repeat;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float tl = GetTextureOffset(tex, x, y, -1, -1);// top left
                    float l = GetTextureOffset(tex, x, y, -1, 0);  // left
                    float bl = GetTextureOffset(tex, x, y, -1, 1); // bottom left
                    float t = GetTextureOffset(tex, x, y, 0, -1);  // top
                    float b = GetTextureOffset(tex, x, y, 0, 1);   // bottom
                    float tr = GetTextureOffset(tex, x, y, 1, -1); // top right
                    float r = GetTextureOffset(tex, x, y, 1, 0);   // right
                    float br = GetTextureOffset(tex, x, y, 1, 1);  // bottom right

                    // Compute dx using Sobel:
                    //           -1 0 1 
                    //           -2 0 2
                    //           -1 0 1

                    float dX = tr + 2 * r + br - tl - 2 * l - bl;

                    // Compute dy using Sobel:
                    //           -1 -2 -1 
                    //            0  0  0
                    //            1  2  1
                    float dY = bl + 2 * b + br - tl - 2 * t - tr;

                    Vector3 normal = new Vector3(dX, normalStrength, dY).normalized;

                    normal = (normal * 0.5f) + new Vector3(0.5f, 0.5f, 0.5f);

                    newTex.SetPixel(x, y, new Color(1.0f - normal.x, 1.0f - normal.z, 1.0f - normal.y));
                }
            }

            newTex.Apply();
            imageData = newTex.EncodeToPNG();
            File.WriteAllBytes(fileFullPath, imageData);
        }

        static float GetTextureOffset(Texture2D tex, int x, int y, int offSetX, int offsetY)
        {
            return tex.GetPixel(x + offSetX, y + offsetY).grayscale;
        }

        #endregion

        //static void WriteMaterial(BinaryWriter bw, Material mat)
        //{
        //    string path = AssetDatabase.GetAssetPath(mat.shader.GetInstanceID());

        //    if (path == "")
        //    {
        //        Debug.Log("Built-in shader");
        //        switch (mat.shader.name)
        //        {
        //            case "Specular":
        //                bw.Write((int)PartToolsLib.ShaderType.Specular);

        //                AddTexture(bw, mat, "_MainTex");
        //                WriteColor(bw, mat.GetColor("_Color"));
        //                WriteColor(bw, mat.GetColor("_SpecColor"));
        //                bw.Write(mat.GetFloat("_Shininess"));

        //                break;

        //            case "Bumped Diffuse":
        //                bw.Write((int)PartToolsLib.ShaderType.BumpedDiffuse);

        //                AddTexture(bw, mat, "_MainTex");
        //                AddTexture(bw, mat, "_BumpMap");
        //                WriteColor(bw, mat.GetColor("_Color"));

        //                break;

        //            case "Bumped Specular":
        //                bw.Write((int)PartToolsLib.ShaderType.BumpedSpecular);

        //                AddTexture(bw, mat, "_MainTex");
        //                AddTexture(bw, mat, "_BumpMap");
        //                WriteColor(bw, mat.GetColor("_Color"));
        //                WriteColor(bw, mat.GetColor("_SpecColor"));
        //                bw.Write(mat.GetFloat("_Shininess"));

        //                break;

        //            case "Diffuse":
        //            default:
        //                bw.Write((int)PartToolsLib.ShaderType.Diffuse);

        //                AddTexture(bw, mat, "_MainTex");
        //                WriteColor(bw, mat.GetColor("_Color"));

        //                break;
        //        }
        //    }
        //    else
        //    {
        //        Debug.Log("Custom shader");
        //        bw.Write((int)PartToolsLib.ShaderType.Custom);
        //    }

        //    AssetImporter shaderAsset = AssetImporter.GetAtPath(path);
        //    Debug.Log(path + " " + shaderAsset);
        //    Shader shader = materal.shader;
        //    Debug.Log(shader.name);

        //    //// parse properties
        //    //int braceIndex = shaderText.IndexOf("{", 0);
        //    //int propStart = shaderText.IndexOf("{", braceIndex);

        //    //while (shaderText.IndexOf("}", propStart) < shaderText.IndexOf("{", propStart))
        //    //{
        //    //    int propEnd = shaderText.IndexOf("}");
        //    //    string prop = shaderText.Substring(propStart, propEnd);
        //    //    Debug.Log(prop);
        //    //}
        //}
    }

    private static class BitmapWriter
    {
        public static bool Write2D(Texture texture, string newPath)
        {
            return Write2D(texture, newPath, PartToolsLib.TextureType.Texture);
        }

        public static bool Write2D(Texture texture, string newPath, PartToolsLib.TextureType texType)
        {
            string oldPath = AssetDatabase.GetAssetPath(texture);

            TextureImporter texImporter = (TextureImporter)AssetImporter.GetAtPath(oldPath);

            TextureImporterSettings texSettingsBackup = new TextureImporterSettings();
            texImporter.ReadTextureSettings(texSettingsBackup);


            if (texImporter == null)
                return false;

            texImporter.isReadable = true;
            texImporter.mipmapEnabled = false;

            bool wasNormalMap = texImporter.convertToNormalmap;
            texImporter.convertToNormalmap = false;

            // force the update of the above settings
            AssetDatabase.ImportAsset(oldPath, ImportAssetOptions.ForceUpdate);


            if (texType == PartToolsLib.TextureType.NormalMap && wasNormalMap)
            {
                WriteTexture2D(ConvertTextureToNormal((Texture2D)texture, texImporter.heightmapScale), texType, newPath);
            }
            else
                WriteTexture2D((Texture2D)texture, texType, newPath);


            // reapply the old settings
            texImporter.SetTextureSettings(texSettingsBackup);
            AssetDatabase.ImportAsset(oldPath, ImportAssetOptions.ForceUpdate);

            return true;
        }


        private static void WriteTexture2D(Texture2D texture, PartToolsLib.TextureType texType, string newPath)
        {
            BinaryWriter bw = new BinaryWriter(File.Open(newPath, FileMode.Create));

            // write header
            bw.Write("KSP");
            bw.Write(texture.width);
            bw.Write(texture.height);
            bw.Write((int)texType);


            Color32[] colors = texture.GetPixels32(0);
            int nPixels = colors.Length;
            Color32 color;

            if (texType == PartToolsLib.TextureType.Texture)
            {
                bool writeAlpha = false;
                switch (texture.format)
                {
                    case TextureFormat.ARGB32:
                    case TextureFormat.RGBA32:
                    case TextureFormat.DXT5:

                        writeAlpha = true;
                        bw.Write(32);

                        break;
                    case TextureFormat.DXT1:
                    case TextureFormat.RGB24:

                        writeAlpha = false;
                        bw.Write(24);

                        break;
                }

                for (int i = 0; i < nPixels; i++)
                {
                    color = colors[i];

                    bw.Write(color.r);
                    bw.Write(color.g);
                    bw.Write(color.b);

                    if (writeAlpha)
                        bw.Write(color.a);

                    if (i % texture.height == 0)
                        bw.Flush();
                }
            }
            else if (texType == PartToolsLib.TextureType.NormalMap)
            {
                bw.Write(32);

                for (int i = 0; i < nPixels; i++)
                {
                    color = colors[i];

                    bw.Write(color.r);
                    bw.Write(color.g);
                    bw.Write(color.b);
                    bw.Write(color.a);

                    if (i % texture.height == 0)
                        bw.Flush();
                }
            }

            bw.Close();
        }

        private static Texture2D ConvertTextureToNormal(Texture2D tex, float normalStrength)
        {
            int width = tex.width;
            int height = tex.height;

            Texture2D newTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            newTex.wrapMode = TextureWrapMode.Repeat;

            Color color;

            float nStrength = normalStrength * 0.001f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float tl = GetTextureOffset(tex, x, y, -1, -1);// top left
                    float l = GetTextureOffset(tex, x, y, -1, 0);  // left
                    float bl = GetTextureOffset(tex, x, y, -1, 1); // bottom left
                    float t = GetTextureOffset(tex, x, y, 0, -1);  // top
                    float b = GetTextureOffset(tex, x, y, 0, 1);   // bottom
                    float tr = GetTextureOffset(tex, x, y, 1, -1); // top right
                    float r = GetTextureOffset(tex, x, y, 1, 0);   // right
                    float br = GetTextureOffset(tex, x, y, 1, 1);  // bottom right

                    // Compute dx using Sobel:
                    //           -1 0 1 
                    //           -2 0 2
                    //           -1 0 1

                    float dX = tr + 2 * r + br - tl - 2 * l - bl;

                    // Compute dy using Sobel:
                    //           -1 -2 -1 
                    //            0  0  0
                    //            1  2  1
                    float dY = bl + 2 * b + br - tl - 2 * t - tr;


                    Vector3 normal = new Vector3(dX, nStrength, dY).normalized;

                    normal = (normal * 0.5f) + new Vector3(0.5f, 0.5f, 0.5f);

                    color.r = 1.0f - normal.y;
                    color.g = 1.0f - normal.z;
                    color.b = 1.0f;
                    color.a = 1.0f - normal.x;

                    newTex.SetPixel(x, y, color);
                }
            }

            newTex.Apply();

            return newTex;
        }

        private static float GetTextureOffset(Texture2D tex, int x, int y, int offSetX, int offsetY)
        {
            return tex.GetPixel(x + offSetX, y + offsetY).grayscale;
        }
    }

}