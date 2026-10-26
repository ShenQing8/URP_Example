using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    public Camera camera;
    public List<Transform> allCameraTransforms = new List<Transform>();

    int index = 0;
    public int CurrentViewIndex => index;
    public void NextView()
    {
        if (allCameraTransforms.Count > 0) index = (index + 1) % allCameraTransforms.Count;
    }
    private void OnGUI()
    {
        using var gui = DemoGUI.Begin();
        if(GUI.Button(new Rect(100, 450, 200, 100), "Move camera"))
        {
            NextView();
        }
    }

    private void LateUpdate()
    {
        if (!camera || allCameraTransforms.Count == 0) return;
        index %= allCameraTransforms.Count;
        Transform target = allCameraTransforms[index];
        if (!target) return;

        camera.transform.position = Vector3.Lerp(camera.transform.position,target.position, Time.deltaTime * 2);
        camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation,target.rotation, Time.deltaTime * 2);
    }
}
