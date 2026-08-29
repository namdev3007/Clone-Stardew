using UnityEngine.Serialization;

namespace Plugins.Lowscope.ComponentSaveSystem.Components.Scriptableobjects
{
    [System.Serializable]
    public class SaveIdentifierReference
    {
        public bool UseConstant = true;

        [FormerlySerializedAs("saveIdentification")]
        public string ConstantValue = "";
        public SaveIdentifierVariable Variable;

        public SaveIdentifierReference()
        { }

        public SaveIdentifierReference(string value)
        {
            UseConstant = true;
            ConstantValue = value;
        }

        public string Value
        {
            get { return UseConstant ? ConstantValue : Variable != null ? Variable.Identifier : ""; }
        }

        public static implicit operator string(SaveIdentifierReference reference)
        {
            return reference.Value;
        }
    }
}