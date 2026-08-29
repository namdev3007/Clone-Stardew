using Referencing.Scriptable_Variables.Variables;
using System;

namespace Referencing.Scriptable_Variables.References
{
    [Serializable]
    public class IntReference
    {
        public bool UseConstant = true;
        public int ConstantValue;
        public IntVariable Variable;

        public IntReference() { }

        public IntReference(int value)
        {
            UseConstant = true;
            ConstantValue = value;
        }

        public int Value
        {
            get { return UseConstant ? ConstantValue : Variable.Value; }
        }
    }
}