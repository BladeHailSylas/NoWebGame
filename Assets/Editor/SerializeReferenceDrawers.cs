using Moves;
using Moves.ObjectEntity;
using UnityEditor;

namespace Editor
{
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

    [CustomPropertyDrawer(typeof(MechanismData), true)]
    public class SkillDataDrawer : SerializeReferenceDrawerBase<MechanismData>
    {
        protected override string DropdownLabel => "Skill Type";
        protected override string FieldLabel => "Skill Data";
    }
}