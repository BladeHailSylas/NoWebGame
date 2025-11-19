using Moves;
using Moves.ObjectEntity;
using UnityEditor;

namespace Editor
{
    [CustomPropertyDrawer(typeof(INewParams), true)]
    public class INewParamsDrawer : SerializeReferenceDrawerBase<INewParams>
    {
        protected override string DropdownLabel => "Param Type";
        protected override string FieldLabel => "Param Data";
    }

    [CustomPropertyDrawer(typeof(IAreaShapes), true)]
    public class IAreaShapesDrawer : SerializeReferenceDrawerBase<IAreaShapes>
    {
        protected override string DropdownLabel => "Shape Type";
        protected override string FieldLabel => "Shape Data";
    }
}