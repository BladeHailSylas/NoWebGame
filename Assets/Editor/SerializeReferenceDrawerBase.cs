using System;
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
        private Type[] _cachedTypes;
        private string[] _cachedTypeNames;

        protected abstract string DropdownLabel { get; }
        protected abstract string FieldLabel { get; }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (_cachedTypes == null)
            {
                // 표시명이 같아도 서로 다른 타입이므로 실제 Type을 선택 기준으로 유지합니다.
                var types = (from asm in AppDomain.CurrentDomain.GetAssemblies()
                    from type in asm.GetTypes()
                    where !type.IsAbstract && typeof(T).IsAssignableFrom(type)
                    select type).Distinct()
                    .OrderBy(type => type.Name, StringComparer.Ordinal)
                    .ThenBy(type => type.FullName, StringComparer.Ordinal)
                    .ThenBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
                    .ToArray();

                var nameCounts = types.GroupBy(type => type.Name)
                    .ToDictionary(group => group.Key, group => group.Count());
                var fullNameCounts = types.GroupBy(type => type.FullName ?? type.Name)
                    .ToDictionary(group => group.Key, group => group.Count());

                // 동명이면 중첩 클래스 경로를, 전체 이름까지 같으면 어셈블리도 표시합니다.
                _cachedTypeNames = types.Select(type =>
                {
                    var fullName = type.FullName ?? type.Name;
                    if (nameCounts[type.Name] == 1) return type.Name;
                    if (fullNameCounts[fullName] == 1) return fullName;
                    return type.AssemblyQualifiedName ?? fullName;
                }).ToArray();
                _cachedTypes = types;
            }

            EditorGUI.BeginProperty(position, label, property);

            var currentType = property.managedReferenceValue?.GetType();
            var currentIndex = currentType != null ? Array.IndexOf(_cachedTypes, currentType) : -1;

            var dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var fieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
                position.width, position.height - EditorGUIUtility.singleLineHeight - 2);

            int newIndex = EditorGUI.Popup(dropdownRect, DropdownLabel, currentIndex, _cachedTypeNames);
            if (newIndex != currentIndex && newIndex >= 0 && newIndex < _cachedTypes.Length)
            {
                var type = _cachedTypes[newIndex];
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
