using SkillInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ISkillParam), true)]
public class SerializeReferenceDrawer : PropertyDrawer
{
    private Dictionary<string, Type> _cachedTypes;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        _cachedTypes ??= (from asm in AppDomain.CurrentDomain.GetAssemblies()
                            from type in asm.GetTypes()
                            where !type.IsAbstract && typeof(ISkillParam).IsAssignableFrom(type)
                            select type).ToDictionary(t => t.Name, t => t);

        // 라벨 표시
        EditorGUI.BeginProperty(position, label, property);
        var typeNames = _cachedTypes.Keys.ToList();

        // 현재 타입
        var currentType = property.managedReferenceValue?.GetType();
        var currentIndex = currentType != null ? typeNames.IndexOf(currentType.Name) : -1;

        // 드롭다운 Rect 분리
        var dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var fieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
                                 position.width, position.height - EditorGUIUtility.singleLineHeight - 2);

        // 드롭다운
        int newIndex = EditorGUI.Popup(dropdownRect, "Param Type", currentIndex, typeNames.ToArray());
        if (newIndex != currentIndex)
        {
            var type = _cachedTypes[typeNames[newIndex]];
            property.managedReferenceValue = Activator.CreateInstance(type);
        }

        // 실제 필드 표시
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
