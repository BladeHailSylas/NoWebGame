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
    
    [CustomPropertyDrawer(typeof(INewMechanism), true)]
    public class INewMechanismDrawer : SerializeReferenceDrawerBase<INewMechanism>
    {
        protected override string DropdownLabel => "Mechanism Type";
        protected override string FieldLabel => "Mechanism Data";
    }
    
    [CustomPropertyDrawer(typeof(NewMechanism), true)]
    public class NewMechanismDrawer : SerializeReferenceDrawerBase<NewMechanism>
    {
        protected override string DropdownLabel => "Mechanism Type";
        protected override string FieldLabel => "Mechanism Data";
    }

    [CustomPropertyDrawer(typeof(IAreaShapes), true)]
    public class IAreaShapesDrawer : SerializeReferenceDrawerBase<IAreaShapes>
    {
        protected override string DropdownLabel => "Shape Type";
        protected override string FieldLabel => "Shape Data";
    }
}