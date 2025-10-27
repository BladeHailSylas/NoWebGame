using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(INewParams), true)]
public class SerializeReferenceDrawer : PropertyDrawer
{
    private Dictionary<string, Type> _cachedTypes;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        _cachedTypes ??= (from asm in AppDomain.CurrentDomain.GetAssemblies()
                            from type in asm.GetTypes()
                            where !type.IsAbstract && typeof(INewParams).IsAssignableFrom(type)
                            select type).ToDictionary(t => t.Name, t => t);

        //
        EditorGUI.BeginProperty(position, label, property);
        var typeNames = _cachedTypes.Keys.ToList();

        //
        var currentType = property.managedReferenceValue?.GetType();
        var currentIndex = currentType != null ? typeNames.IndexOf(currentType.Name) : -1;

        //
        var dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var fieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
                                 position.width, position.height - EditorGUIUtility.singleLineHeight - 2);

        //
        int newIndex = EditorGUI.Popup(dropdownRect, "Param Type", currentIndex, typeNames.ToArray());
        if (newIndex != currentIndex)
        {
            var type = _cachedTypes[typeNames[newIndex]];
            property.managedReferenceValue = Activator.CreateInstance(type);
        }

        //
        if (property.managedReferenceValue != null)
            EditorGUI.PropertyField(fieldRect, property, new GUIContent("Param Data"), true);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float baseHeight = EditorGUIUtility.singleLineHeight + 4;
        if (property.managedReferenceValue != null)
            baseHeight += EditorGUI.GetPropertyHeight(property, true);
        return baseHeight;
    }
}
