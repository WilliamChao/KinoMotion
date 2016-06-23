//
// Kineblur - Motion blur post effect for Unity.
//
// Copyright (C) 2015 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(Kineblur)), CanEditMultipleObjects]
public class KineblurEditor : Editor
{
    SerializedProperty propExposureTime;
    SerializedProperty propVelocityFilter;
    SerializedProperty propDepthFilter;
    SerializedProperty propDepthFilterOffset;
    SerializedProperty propSampleCount;
    SerializedProperty propDither;
    SerializedProperty propDebug;

    GUIContent labelDebug;

    static int[] exposureOptions = { 0, 1, 2, 3, 4 };

    static GUIContent[] exposureOptionLabels = {
        new GUIContent("Realtime"),
        new GUIContent("1 \u2044 15"),
        new GUIContent("1 \u2044 30"),
        new GUIContent("1 \u2044 60"),
        new GUIContent("1 \u2044 125")
    };

    void OnEnable()
    {
        propExposureTime = serializedObject.FindProperty("_exposureTime");
        propVelocityFilter = serializedObject.FindProperty("_velocityFilter");
        propDepthFilter = serializedObject.FindProperty("_depthFilter");
        propDepthFilterOffset = serializedObject.FindProperty("_depthFilterOffset");
        propSampleCount = serializedObject.FindProperty("_sampleCount");
        propDither = serializedObject.FindProperty("_dither");
        propDebug = serializedObject.FindProperty("_debug");
        labelDebug = new GUIContent("Visualize Velocity");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.IntPopup(propExposureTime, exposureOptionLabels, exposureOptions);

        EditorGUILayout.PropertyField(propVelocityFilter);

        EditorGUILayout.PropertyField(propDepthFilter);
        if (propDepthFilter.hasMultipleDifferentValues || propDepthFilter.boolValue)
            EditorGUILayout.Slider(propDepthFilterOffset, 0.0001f, 0.01f);

        EditorGUILayout.PropertyField(propSampleCount);

        EditorGUILayout.PropertyField(propDither);
        EditorGUILayout.PropertyField(propDebug, labelDebug);

        serializedObject.ApplyModifiedProperties();
    }
}
