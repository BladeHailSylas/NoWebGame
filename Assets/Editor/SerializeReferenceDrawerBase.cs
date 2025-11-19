using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// SerializeReference 타입 선택 전용 Generic Drawer
    /// </summary>
    public abstract class SerializeReferenceDrawerBase<T> : PropertyDrawer
    {
        private Dictionary<string, Type> _cachedTypes;

        protected abstract string DropdownLabel { get; }
        protected abstract string FieldLabel { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            _cachedTypes ??= (from asm in AppDomain.CurrentDomain.GetAssemblies()
                from type in asm.GetTypes()
                where !type.IsAbstract && typeof(T).IsAssignableFrom(type)
                select type).ToDictionary(t => t.Name, t => t);

            EditorGUI.BeginProperty(position, label, property);
            var typeNames = _cachedTypes.Keys.ToList();

            var currentType = property.managedReferenceValue?.GetType();
            var currentIndex = currentType != null ? typeNames.IndexOf(currentType.Name) : -1;

            var dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var fieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
                position.width, position.height - EditorGUIUtility.singleLineHeight - 2);

            int newIndex = EditorGUI.Popup(dropdownRect, DropdownLabel, currentIndex, typeNames.ToArray());
            if (newIndex != currentIndex)
            {
                var type = _cachedTypes[typeNames[newIndex]];
                property.managedReferenceValue = Activator.CreateInstance(type);
            }

            if (property.managedReferenceValue != null)
                EditorGUI.PropertyField(fieldRect, property, new GUIContent(FieldLabel), true);

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
}