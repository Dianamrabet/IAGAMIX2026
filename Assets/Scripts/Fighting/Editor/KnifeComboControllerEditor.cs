using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(KnifeComboController))]
public class KnifeComboControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        KnifeComboController comboController = (KnifeComboController)target;

        GUILayout.Space(10);
        GUILayout.Label("Play Combos", EditorStyles.boldLabel);

        if (GUILayout.Button("Combo 1: Quick Slash"))
            comboController.PlayCombo1();
        if (GUILayout.Button("Combo 2: Fast Jab"))
            comboController.PlayCombo2();
        if (GUILayout.Button("Combo 3: Power Strike"))
            comboController.PlayCombo3();
    }
}
