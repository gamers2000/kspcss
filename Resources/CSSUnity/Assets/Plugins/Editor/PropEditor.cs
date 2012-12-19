using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[CustomEditor(typeof(PropObject))]
public class PropEditor : Editor
{
    public PropObject Target { get { return (PropObject)target; } }

    private static GUILayoutOption colLabel = GUILayout.Width(100);

    public override void OnInspectorGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Prop", colLabel);
        GUILayout.Label(Target.prop.propName);
        GUILayout.EndHorizontal();

        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap Down"))
        {
            Snap();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Orient Down"))
        {
            Orient();
        }
        GUILayout.EndHorizontal();
    }

    private void Snap()
    {
        RaycastHit hit = new RaycastHit();

        Vector3 direction = -Target.transform.up;

        if (Physics.Raycast(new Ray(Target.transform.position - (direction * 0.01f), direction), out hit, float.MaxValue))
        {
            Target.transform.position = hit.point;
            Debug.Log("Snapped to point " + hit.point);
        }
        else
        {
            Debug.Log("Unable to snap: Suitable collider not found");
        }
    }

    private void Orient()
    {
        RaycastHit hit = new RaycastHit();

        Vector3 direction = -Target.transform.up;
        if (Physics.Raycast(new Ray(Target.transform.position - (direction * 0.01f), direction), out hit, float.MaxValue))
        {
            Target.transform.up = hit.normal;
            Debug.Log("Orientated to normal " + hit.normal);
        }
        else
        {
            Debug.Log("Unable to orient: Suitable collider not found");
        }
    }
}