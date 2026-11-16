using Moves;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomPropertyDrawer(typeof(DestinationSettings))]
    public sealed class DestinationSettingsDrawer : PropertyDrawer
    {
        private static bool Supported(SerializedProperty property)
        {
            var index = property.propertyPath.LastIndexOf('.');
            if (index < 0) return true;
            var parent = property.serializedObject.FindProperty(property.propertyPath.Substring(0, index));
            return parent == null || parent.propertyType != SerializedPropertyType.ManagedReference ||
                parent.managedReferenceValue is INewMechanism mech && SpatialExecution.Supports(mech);
        }
        private static bool Active(SerializedProperty p) => p.FindPropertyRelative("mode").intValue != 0;
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!Supported(property) && !Active(property)) return 0;
            return (EditorGUIUtility.singleLineHeight + 2) * (Active(property) ? 6 : 1) + (Active(property) ? 42 : 0);
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!Supported(property) && !Active(property)) return;
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            void Field(SerializedProperty p, string text)
            { EditorGUI.PropertyField(line, p, new GUIContent(text)); line.y += line.height + 2; }
            Field(property.FindPropertyRelative("mode"), "Destination");
            if (Active(property))
            {
                var data = property.FindPropertyRelative("position");
                Field(data.FindPropertyRelative("representation"), "Representation");
                Field(data.FindPropertyRelative("anchor"), "Base Position");
                var distance = data.FindPropertyRelative("distance");
                Field(distance, "Distance (world units)");
                using (new EditorGUI.DisabledScope(distance.floatValue == 0))
                {
                    Field(data.FindPropertyRelative("direction"), "Base Direction");
                    Field(data.FindPropertyRelative("angleDegrees"), "Angle (degrees, CCW +)");
                }
                var anchor = data.FindPropertyRelative("anchor").intValue;
                var direction = data.FindPropertyRelative("direction").intValue;
                var invalid = !Supported(property) || property.FindPropertyRelative("mode").intValue != 1 ||
                    data.FindPropertyRelative("representation").intValue != 0 || anchor < 0 || anchor > 3 ||
                    !BasePositionResolver.Finite(distance.floatValue) || distance.floatValue < 0 ||
                    distance.floatValue > 0 && (direction < 0 || direction > 2 || !BasePositionResolver.Finite(data.FindPropertyRelative("angleDegrees").floatValue));
                EditorGUI.HelpBox(new Rect(line.x, line.y, line.width, 40), invalid
                    ? "지원하지 않는 Mechanism 또는 잘못된 공간 설정입니다. 실행 전에 Abort됩니다."
                    : "실행 직전 고정 좌표를 계산합니다. InputTarget은 최종 Target이며, ExitPosition에는 종료 문맥이 필요합니다.",
                    invalid ? MessageType.Error : MessageType.Info);
            }
            EditorGUI.EndProperty();
        }
    }
}
