using Moves;
using Systems.Data;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class TargetMaskEditorDefaults
    {
        public static object Create(System.Type type)
        {
            var instance = System.Activator.CreateInstance(type);
            if (instance is NewMechanism mechanism) mechanism.ConfigureRelativeTargets(TargetRelationMask.None);
            return instance;
        }
    }
    [CustomPropertyDrawer(typeof(TargetRelationMask))]
    public sealed class TargetRelationMaskDrawer : PropertyDrawer
    {
        private static readonly string[] Names = { "Self", "Ally", "Enemy", "Neutral", "Unrelated" };
        // Unity MaskField의 Everything(-1)을 유효한 다섯 비트로 정규화합니다. Unknown 비트는 없습니다.
        public static int FromSelection(int selected) => selected & 31;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.MaskField(line, new GUIContent(label.text, "Caster 기준 관계. None은 모든 Entity 효과를 거부합니다. Unknown은 선택할 수 없습니다."), property.intValue, Names);
            if (EditorGUI.EndChangeCheck()) property.intValue = FromSelection(selected);
            if (!TargetRelationMasks.IsValid((TargetRelationMask)property.intValue))
                EditorGUI.HelpBox(new Rect(position.x, line.yMax + 2, position.width, 36), "Invalid relation bits: select supported relationships.", MessageType.Error);
            EditorGUI.EndProperty();
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight + (TargetRelationMasks.IsValid((TargetRelationMask)property.intValue) ? 0 : 38);
    }

    [CustomPropertyDrawer(typeof(TargetMaskVersionAttribute))]
    public sealed class TargetMaskVersionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var text = property.intValue == 0 ? "Legacy — migration required" :
                property.intValue == 1 ? "Relative (v1) — physical candidates: All Layers" : "Unsupported version " + property.intValue;
            EditorGUI.LabelField(position, new GUIContent("Target Policy", "이 값은 변환 도구가 관리합니다. Legacy Layer Mask와 관계 Flags는 별개입니다."), new GUIContent(text));
        }
    }
}
