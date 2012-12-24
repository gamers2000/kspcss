using System;
using UnityEngine;

namespace KSPCSS
{
    class RotationModule : PartModule
    {
        public override void OnUpdate()
        {
            if (Input.GetKey(KeyCode.E))
            {
                
                Transform obj = part.FindModelTransform("cargobay");
                //Transform pivot = part.FindModelTransform("GEO_CargoBay07");
                /*obj.Rotate(Vector3.up * 10, Space.Self);
                obj.Rotate(Vector3.right * 10, Space.Self);
                obj.Rotate(Vector3.forward * 10, Space.Self);*/
                obj.eulerAngles = obj.eulerAngles + new Vector3(1, 1, 1);
                PDebug.Log(obj.localEulerAngles.x);
                PDebug.Log(obj.localEulerAngles.y);
                PDebug.Log(obj.localEulerAngles.z);
            }
            if (Input.GetKey(KeyCode.W))
            {
                PDebug.Log("W");
                this.transform.Rotate(Vector3.up * 10, Space.Self);
            }
        }
    }
}
