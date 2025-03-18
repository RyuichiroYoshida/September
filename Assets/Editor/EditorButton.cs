using UnityEngine;
using UnityEditor;
public class EditorButton : EditorWindow
{
    [MenuItem("Window/EditorSampleButton")]
    public static void ShowWindow()
    {
        var window = EditorWindow.GetWindow<EditorButton>("EditorSampleButton");
        window.Show();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Test"))
        {
            Debug.Log("Clicked");  
            
        }
    }
    
}
