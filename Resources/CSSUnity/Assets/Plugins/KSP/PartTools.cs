using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

[AddComponentMenu("KSP/Part Tools")]
public class PartTools : MonoBehaviour
{
    public string modelName;
    public string filePath;
    public string filename;

    public bool copyTexturesToOutputDirectory;
    public bool autoRenameTextures;
    public bool convertTextures;

    [System.Serializable]
    public class ModelPartEvent
    {
        public string eventStart;

        public string code;

        public string eventFinish;

        public ModelPartEvent()
        {
            eventStart = "";
            code = "";
            eventFinish = "";
        }
    }

    void Reset()
    {
        modelName = "NewModel";
        filePath = "Parts/NewPart/";
        filename = "model";

        copyTexturesToOutputDirectory = true;
        autoRenameTextures = true;
        convertTextures = true;
    }
}